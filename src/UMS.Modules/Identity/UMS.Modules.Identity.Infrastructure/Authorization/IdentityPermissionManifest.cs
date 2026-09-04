using UMS.Modules.Identity.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Identity.Infrastructure.Authorization;

/// <summary>Identity's own contribution to the platform-wide Permission catalog (requirement-spec.md identity §2's example table).</summary>
internal sealed class IdentityPermissionManifest : IPermissionManifest
{
    public string OwningModule => "identity";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(IdentityPermissions.UserRead, "View User accounts."),
        new(IdentityPermissions.UserManage, "Provision and suspend/reactivate User accounts."),
        new(IdentityPermissions.RoleManage, "Create Roles and manage their Permission bundles."),
        new(IdentityPermissions.RoleAssign, "Assign or revoke a Role (with an optional ScopeGrant) on a User."),
        new(IdentityPermissions.SessionRead, "View a User's own active Sessions."),
        new(IdentityPermissions.SessionRevoke, "Revoke a User's own Session."),
        new(IdentityPermissions.PermissionRead, "View the registered Permission catalog."),
    ];
}
