using Microsoft.EntityFrameworkCore;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Permissions;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class PermissionCatalogRepository(IdentityDbContext context) : IPermissionCatalogRepository
{
    public async Task<IReadOnlyList<PermissionCatalogEntry>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.Permissions.OrderBy(p => p.Key).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlySet<string>> GetAllKeysAsync(CancellationToken cancellationToken = default) =>
        (await context.Permissions.Select(p => p.Key).ToListAsync(cancellationToken).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);

    public async Task UpsertManyAsync(IEnumerable<PermissionCatalogEntry> entries, CancellationToken cancellationToken = default)
    {
        var entryList = entries.ToList();
        if (entryList.Count == 0)
        {
            return;
        }

        var keys = entryList.Select(e => e.Key).ToList();
        var existing = await context.Permissions
            .Where(p => keys.Contains(p.Key))
            .ToDictionaryAsync(p => p.Key, cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in entryList)
        {
            if (existing.TryGetValue(entry.Key, out var current))
            {
                current.UpdateMetadata(entry.OwningModule, entry.Description, entry.RegisteredAt);
            }
            else
            {
                context.Permissions.Add(entry);
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
