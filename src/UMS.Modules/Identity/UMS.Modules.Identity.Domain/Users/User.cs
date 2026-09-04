using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Events;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.ScopeGrants;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.Domain.Users;

/// <summary>
/// One authenticatable identity, shared across all six applications regardless of how many Roles
/// held (glossary, requirement-spec.md identity §3/§4 "One User, one identity"). Every invariant
/// named in the requirement spec is enforced here, on the aggregate itself, never by an external
/// service mutating public setters (ums-conventions.md, Domain Modeling).
/// </summary>
public sealed class User : AggregateRoot<UserId>
{
    private readonly List<UserRoleAssignment> _roleAssignments = [];

    private User()
    {
    }

    private User(
        UserId id,
        string username,
        Email email,
        PersonName name,
        PhoneNumber? mobile,
        string? universityId,
        Credential credential,
        DateTimeOffset now)
    {
        Id = id;
        Username = username;
        Email = email;
        Name = name;
        Mobile = mobile;
        UniversityId = universityId;
        Credential = credential;
        Status = UserStatus.Active;
        CreatedAt = now;
    }

    public string Username { get; private set; } = string.Empty;

    public Email Email { get; private set; } = null!;

    public PersonName Name { get; private set; } = null!;

    public PhoneNumber? Mobile { get; private set; }

    /// <summary>
    /// Absent for a User who is not yet an enrolled Student/staff member - a not-yet-enrolled
    /// Applicant has no University ID at all and is out of Identity's scope until provisioned
    /// (requirement-spec.md identity §8).
    /// </summary>
    public string? UniversityId { get; private set; }

    public Credential Credential { get; private set; } = null!;

    public UserStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SuspendedAt { get; private set; }

    public IReadOnlyCollection<UserRoleAssignment> RoleAssignments => _roleAssignments.AsReadOnly();

    /// <summary>
    /// Creates a brand-new <see cref="User"/> row. Callers (the application service) are
    /// responsible for the "look up existing User by stable identifier before creating" half of
    /// the "Same person, two roles, one identity" invariant (§8) - this factory always creates,
    /// it never searches, matching the aggregate's own responsibility boundary.
    /// </summary>
    public static User Provision(
        string username,
        Email email,
        PersonName name,
        PhoneNumber? mobile,
        string? universityId,
        Credential credential,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username is required to provision a User.", nameof(username));
        }

        var user = new User(UserId.New(), username.Trim(), email, name, mobile, universityId, credential, now);
        user.Raise(new UserRegistered(user.Id, user.Email.Value, now));
        return user;
    }

    public void ChangePassword(Credential newCredential, DateTimeOffset now)
    {
        Credential = newCredential;
        Raise(new PasswordChanged(Id, now));
    }

    public void Suspend(DateTimeOffset now)
    {
        if (Status == UserStatus.Suspended)
        {
            throw new InvalidOperationException("User is already suspended.");
        }

        Status = UserStatus.Suspended;
        SuspendedAt = now;
        Raise(new UserStatusChanged(Id, Status, now));
    }

    public void Reactivate(DateTimeOffset now)
    {
        if (Status == UserStatus.Active)
        {
            throw new InvalidOperationException("User is already active.");
        }

        Status = UserStatus.Active;
        SuspendedAt = null;
        Raise(new UserStatusChanged(Id, Status, now));
    }

    /// <summary>
    /// Adds a (Role, optional ScopeGrant) binding (requirement-spec.md identity §6
    /// `POST /users/{id}/roles`). Succeeds even if <paramref name="roleId"/>'s Role requires MFA
    /// and this User has not enrolled - per §8's "MFA-required role assigned to a User without MFA
    /// enrolled" edge case, that gate is enforced at permission-resolution time, never here.
    /// </summary>
    public UserRoleAssignment AssignRole(RoleId roleId, OrganizationNodeId? scopeNode, DateTimeOffset now)
    {
        var alreadyActive = _roleAssignments.Any(a => a.RoleId == roleId && a.ScopeNode == scopeNode && a.IsActive);
        if (alreadyActive)
        {
            throw new InvalidOperationException("This Role is already assigned to the User at this scope.");
        }

        var assignment = UserRoleAssignment.Create(Id, roleId, scopeNode, now);
        _roleAssignments.Add(assignment);
        Raise(new RoleAssigned(Id, roleId, scopeNode?.Value, now));
        return assignment;
    }

    public void RevokeRole(UserRoleAssignmentId assignmentId, DateTimeOffset now)
    {
        var assignment = _roleAssignments.FirstOrDefault(a => a.Id == assignmentId)
            ?? throw new InvalidOperationException("Role assignment not found on this User.");

        if (!assignment.IsActive)
        {
            throw new InvalidOperationException("Role assignment is already revoked.");
        }

        assignment.Revoke(now);
        Raise(new RoleRevoked(Id, assignment.RoleId, assignment.ScopeNode?.Value, now));
    }
}
