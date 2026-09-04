using UMS.Modules.Identity.Domain.Permissions;

namespace UMS.Modules.Identity.Application.Abstractions;

public interface IPermissionCatalogRepository
{
    public Task<IReadOnlyList<PermissionCatalogEntry>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>All currently-registered keys, for validating a Role's requested Permission bundle (requirement-spec.md identity §2).</summary>
    public Task<IReadOnlySet<string>> GetAllKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>Idempotent - re-registering the same key updates its description/owner rather than duplicating (identity §9.2, manifest-registration).</summary>
    public Task UpsertManyAsync(IEnumerable<PermissionCatalogEntry> entries, CancellationToken cancellationToken = default);
}
