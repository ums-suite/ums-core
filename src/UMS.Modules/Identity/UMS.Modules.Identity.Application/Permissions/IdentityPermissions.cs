namespace UMS.Modules.Identity.Application.Permissions;

/// <summary>
/// Identity's own catalog entries (requirement-spec.md identity §2's example table: "Identity's
/// own" permissions). Shared between the Infrastructure-side manifest (what gets registered into
/// the catalog) and the Api-side endpoint gating (what each endpoint requires), so the two can
/// never drift out of sync with each other.
/// </summary>
public static class IdentityPermissions
{
    public const string UserRead = "identity.user.read";
    public const string UserManage = "identity.user.manage";
    public const string RoleManage = "identity.role.manage";
    public const string RoleAssign = "identity.role.assign";
    public const string SessionRead = "identity.session.read";
    public const string SessionRevoke = "identity.session.revoke";
    public const string PermissionRead = "identity.permission.read";
}
