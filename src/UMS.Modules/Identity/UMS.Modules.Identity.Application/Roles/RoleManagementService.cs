using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Roles;

/// <summary>IDN-14: Role &amp; Permission-bundle management (requirement-spec.md identity §2/§3, §6 `GET/POST /roles`, `PATCH /roles/{id}/permissions`).</summary>
public sealed class RoleManagementService(IRoleRepository roles, IPermissionCatalogRepository catalog, IUnitOfWork unitOfWork, IClock clock)
{
    public static RoleDto ToDto(Role role) => new(role.Id.Value, role.Name, role.Description, role.Permissions, role.RequiresMfa, role.CreatedAt);

    public async Task<Result<RoleDto>> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("role.name_required", "Role name is required.");
        }

        if (await roles.GetByNameAsync(request.Name, cancellationToken).ConfigureAwait(false) is not null)
        {
            return Error.Conflict("role.duplicate_name", $"A Role named '{request.Name}' already exists.");
        }

        var unknownPermissionsResult = await ValidatePermissionsExistAsync(request.Permissions, cancellationToken).ConfigureAwait(false);
        if (unknownPermissionsResult is { } error)
        {
            return error;
        }

        var role = Role.Create(request.Name, request.Description, request.Permissions, clock.UtcNow, request.RequiresMfa);
        roles.Add(role);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToDto(role);
    }

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var all = await roles.ListAsync(cancellationToken).ConfigureAwait(false);
        return all.Select(ToDto).ToList();
    }

    public async Task<Result<RoleDto>> UpdatePermissionsAsync(Guid roleId, UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        var role = await roles.GetByIdAsync(new RoleId(roleId), cancellationToken).ConfigureAwait(false);
        if (role is null)
        {
            return Error.NotFound("role.not_found", $"No Role exists with id '{roleId}'.");
        }

        var unknownPermissionsResult = await ValidatePermissionsExistAsync(request.Permissions, cancellationToken).ConfigureAwait(false);
        if (unknownPermissionsResult is { } error)
        {
            return error;
        }

        role.SetPermissions(request.Permissions);
        if (request.RequiresMfa is { } requiresMfa)
        {
            role.SetRequiresMfa(requiresMfa);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Every currently-assigned holder of this Role must see the updated bundle on their very
        // next permission check, not after a cache TTL (identity §4) - callers that assign this
        // Role invalidate per-user; a bundle-wide change is out of MVP scope for per-user fan-out
        // and is bounded by the cache's own short TTL (design-decisions.md) in the meantime.
        return ToDto(role);
    }

    private async Task<Error?> ValidatePermissionsExistAsync(IReadOnlyCollection<string> permissions, CancellationToken cancellationToken)
    {
        if (permissions.Count == 0)
        {
            return null;
        }

        var known = await catalog.GetAllKeysAsync(cancellationToken).ConfigureAwait(false);
        var unknown = permissions.Where(p => !known.Contains(p)).ToList();

        return unknown.Count > 0
            ? Error.Validation("role.unknown_permission", $"Unknown permission key(s): {string.Join(", ", unknown)}.")
            : null;
    }
}
