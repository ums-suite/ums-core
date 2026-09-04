using UMS.Modules.Identity.Domain.Roles;

namespace UMS.Modules.Identity.Application.Abstractions;

public interface IRoleRepository
{
    public Task<Role?> GetByIdAsync(RoleId id, CancellationToken cancellationToken = default);

    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Role>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Batch lookup for building a token's `roles` claim from a User's active RoleAssignments.</summary>
    public Task<IReadOnlyList<Role>> GetByIdsAsync(IEnumerable<RoleId> ids, CancellationToken cancellationToken = default);

    public void Add(Role role);
}
