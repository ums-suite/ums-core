using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Identity.Application.Permissions;

/// <summary>
/// IDN-1: the Permission catalog manifest registration + `GET /permissions`
/// (requirement-spec.md identity §2/§6/§9.2). <see cref="SynchronizeAsync"/> runs once at
/// startup (called from the Host composition root) and upserts every module's registered
/// <see cref="IPermissionManifest"/> - today, only Identity's own; every future module adds its
/// manifest to DI the same way, with zero change needed here.
/// </summary>
public sealed class PermissionCatalogService(
    IPermissionCatalogRepository catalog,
    IEnumerable<IPermissionManifest> manifests,
    IClock clock)
{
    public Task SynchronizeAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var entries = manifests.SelectMany(manifest =>
            manifest.Permissions.Select(p => PermissionCatalogEntry.Register(p.Key, manifest.OwningModule, p.Description, now)));

        return catalog.UpsertManyAsync(entries, cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionCatalogEntryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entries = await catalog.ListAsync(cancellationToken).ConfigureAwait(false);
        return entries
            .Select(e => new PermissionCatalogEntryDto(e.Key, e.OwningModule, e.Description, e.RegisteredAt))
            .ToList();
    }
}
