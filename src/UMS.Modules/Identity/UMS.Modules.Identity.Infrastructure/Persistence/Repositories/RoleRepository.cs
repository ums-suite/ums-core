using Microsoft.EntityFrameworkCore;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Roles;

namespace UMS.Modules.Identity.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository(IdentityDbContext context) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default) =>
        context.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        context.Roles.FirstOrDefaultAsync(r => r.Name == name, cancellationToken);

    public async Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.Roles.OrderBy(r => r.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<RoleId> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return idList.Count == 0
            ? []
            : await context.Roles.Where(r => idList.Contains(r.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Add(Role role) => context.Roles.Add(role);
}
