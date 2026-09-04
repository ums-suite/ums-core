using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.ScopeGrants;

namespace UMS.Modules.Identity.Domain.Users;

/// <summary>
/// One (Role, optional ScopeGrant) binding held by a <see cref="User"/>
/// (requirement-spec.md identity §2: "A ScopeGrant binds one Role assignment to one
/// OrganizationNode ... so the same Role ... is safely reusable across many departments without
/// granting cross-department visibility"). A <c>null</c> <see cref="ScopeNode"/> means the Role's
/// effect is university-wide, per the Example Role/ScopeGrant table (§2). Lifecycle-owned by
/// <see cref="User"/> - never constructed or revoked from outside it, which is why both members
/// are internal.
/// </summary>
public sealed class UserRoleAssignment
{
    private UserRoleAssignment()
    {
    }

    private UserRoleAssignment(UserRoleAssignmentId id, UserId userId, RoleId roleId, OrganizationNodeId? scopeNode, DateTimeOffset assignedAt)
    {
        Id = id;
        UserId = userId;
        RoleId = roleId;
        ScopeNode = scopeNode;
        AssignedAt = assignedAt;
    }

    public UserRoleAssignmentId Id { get; private set; }

    public UserId UserId { get; private set; }

    public RoleId RoleId { get; private set; }

    public OrganizationNodeId? ScopeNode { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsActive => RevokedAt is null;

    internal static UserRoleAssignment Create(UserId userId, RoleId roleId, OrganizationNodeId? scopeNode, DateTimeOffset now) =>
        new(UserRoleAssignmentId.New(), userId, roleId, scopeNode, now);

    internal void Revoke(DateTimeOffset now)
    {
        RevokedAt = now;
    }
}
