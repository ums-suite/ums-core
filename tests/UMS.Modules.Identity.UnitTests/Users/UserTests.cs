using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.ScopeGrants;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Domain;

namespace UMS.Modules.Identity.UnitTests.Users;

public class UserTests
{
    private static readonly DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static User CreateUser(string email = "jane.doe@example.edu.bd")
    {
        var credential = Credential.FromHash("hashed-value", "argon2id", _now);
        return User.Provision(
            "jane.doe",
            Email.Create(email).Value,
            PersonName.Create("Jane", "Doe").Value,
            mobile: null,
            universityId: null,
            credential,
            _now);
    }

    [Fact]
    public void Provision_raises_a_UserRegistered_event()
    {
        var user = CreateUser();

        var domainEvent = Assert.Single(user.DomainEvents);
        var registered = Assert.IsType<Domain.Events.UserRegistered>(domainEvent);
        Assert.Equal(user.Id, registered.UserId);
    }

    [Fact]
    public void Provision_defaults_a_new_User_to_Active_status()
    {
        var user = CreateUser();

        Assert.Equal(UserStatus.Active, user.Status);
    }

    [Fact]
    public void Suspend_transitions_an_Active_user_to_Suspended_and_raises_UserStatusChanged()
    {
        var user = CreateUser();
        user.ClearDomainEvents();

        user.Suspend(_now.AddMinutes(1));

        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.NotNull(user.SuspendedAt);
        var domainEvent = Assert.Single(user.DomainEvents);
        Assert.Equal(UserStatus.Suspended, Assert.IsType<Domain.Events.UserStatusChanged>(domainEvent).NewStatus);
    }

    [Fact]
    public void Suspend_throws_when_the_user_is_already_suspended()
    {
        var user = CreateUser();
        user.Suspend(_now);

        Assert.Throws<InvalidOperationException>(() => user.Suspend(_now.AddMinutes(1)));
    }

    [Fact]
    public void Reactivate_clears_SuspendedAt_and_transitions_back_to_Active()
    {
        var user = CreateUser();
        user.Suspend(_now);

        user.Reactivate(_now.AddDays(1));

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.SuspendedAt);
    }

    [Fact]
    public void AssignRole_adds_an_active_assignment_and_raises_RoleAssigned()
    {
        var user = CreateUser();
        user.ClearDomainEvents();
        var roleId = RoleId.New();

        var assignment = user.AssignRole(roleId, scopeNode: null, _now);

        Assert.True(assignment.IsActive);
        Assert.Contains(user.RoleAssignments, a => a.Id == assignment.Id);
        var domainEvent = Assert.Single(user.DomainEvents);
        Assert.Equal(roleId, Assert.IsType<Domain.Events.RoleAssigned>(domainEvent).RoleId);
    }

    [Fact]
    public void AssignRole_throws_when_the_same_role_and_scope_is_already_actively_assigned()
    {
        var user = CreateUser();
        var roleId = RoleId.New();
        var scope = new OrganizationNodeId(Guid.NewGuid());
        user.AssignRole(roleId, scope, _now);

        Assert.Throws<InvalidOperationException>(() => user.AssignRole(roleId, scope, _now));
    }

    [Fact]
    public void AssignRole_allows_the_same_role_at_a_different_scope()
    {
        var user = CreateUser();
        var roleId = RoleId.New();

        user.AssignRole(roleId, new OrganizationNodeId(Guid.NewGuid()), _now);
        var second = Record.Exception(() => user.AssignRole(roleId, new OrganizationNodeId(Guid.NewGuid()), _now));

        Assert.Null(second);
        Assert.Equal(2, user.RoleAssignments.Count(a => a.IsActive));
    }

    [Fact]
    public void RevokeRole_marks_the_assignment_inactive_and_raises_RoleRevoked()
    {
        var user = CreateUser();
        var roleId = RoleId.New();
        var assignment = user.AssignRole(roleId, scopeNode: null, _now);
        user.ClearDomainEvents();

        user.RevokeRole(assignment.Id, _now.AddDays(1));

        Assert.False(assignment.IsActive);
        var domainEvent = Assert.Single(user.DomainEvents);
        Assert.Equal(roleId, Assert.IsType<Domain.Events.RoleRevoked>(domainEvent).RoleId);
    }

    [Fact]
    public void RevokeRole_after_revoking_allows_reassigning_the_same_role_and_scope()
    {
        var user = CreateUser();
        var roleId = RoleId.New();
        var assignment = user.AssignRole(roleId, scopeNode: null, _now);
        user.RevokeRole(assignment.Id, _now.AddMinutes(1));

        var exception = Record.Exception(() => user.AssignRole(roleId, scopeNode: null, _now.AddMinutes(2)));

        Assert.Null(exception);
    }

    [Fact]
    public void ChangePassword_replaces_the_Credential_and_raises_PasswordChanged()
    {
        var user = CreateUser();
        user.ClearDomainEvents();
        var newCredential = Credential.FromHash("new-hash", "argon2id", _now.AddDays(1));

        user.ChangePassword(newCredential, _now.AddDays(1));

        Assert.Equal(newCredential, user.Credential);
        Assert.Single(user.DomainEvents);
    }

    [Fact]
    public void LockOut_is_idempotent_and_keeps_the_earliest_timestamp()
    {
        var user = CreateUser();

        user.LockOut(_now);
        user.LockOut(_now.AddMinutes(5));

        Assert.Equal(_now, user.LockedOutAt);
    }

    [Fact]
    public void ClearLockout_removes_the_lockout_flag()
    {
        var user = CreateUser();
        user.LockOut(_now);

        user.ClearLockout();

        Assert.Null(user.LockedOutAt);
    }

    [Fact]
    public void BeginMfaEnrollment_sets_a_pending_secret_that_is_not_yet_enrolled()
    {
        var user = CreateUser();

        user.BeginMfaEnrollment("cipher-text", _now, TimeSpan.FromMinutes(10));

        Assert.True(user.Mfa.HasUnexpiredPendingSecret(_now));
        Assert.False(user.Mfa.IsEnrolled);
    }

    [Fact]
    public void BeginMfaEnrollment_called_twice_replaces_the_prior_pending_secret()
    {
        // edge-cases.md, "MFA enrollment interrupted mid-flow": "a fresh enroll call invalidates any prior unverified secret."
        var user = CreateUser();
        user.BeginMfaEnrollment("first-cipher-text", _now, TimeSpan.FromMinutes(10));

        user.BeginMfaEnrollment("second-cipher-text", _now, TimeSpan.FromMinutes(10));

        Assert.Equal("second-cipher-text", user.Mfa.PendingSecretCipherText);
    }

    [Fact]
    public void CompleteMfaEnrollment_promotes_the_pending_secret_and_raises_MfaEnrolled()
    {
        var user = CreateUser();
        user.BeginMfaEnrollment("cipher-text", _now, TimeSpan.FromMinutes(10));
        user.ClearDomainEvents();

        user.CompleteMfaEnrollment(_now.AddMinutes(1));

        Assert.True(user.Mfa.IsEnrolled);
        Assert.Equal("cipher-text", user.Mfa.EnrolledSecretCipherText);
        Assert.Null(user.Mfa.PendingSecretCipherText);
        var domainEvent = Assert.Single(user.DomainEvents);
        Assert.IsType<Domain.Events.MfaEnrolled>(domainEvent);
    }

    [Fact]
    public void CompleteMfaEnrollment_throws_when_the_pending_secret_has_expired()
    {
        var user = CreateUser();
        user.BeginMfaEnrollment("cipher-text", _now, TimeSpan.FromMinutes(10));

        Assert.Throws<InvalidOperationException>(() => user.CompleteMfaEnrollment(_now.AddMinutes(11)));
    }

    [Fact]
    public void CompleteMfaEnrollment_throws_when_no_enrollment_was_ever_begun()
    {
        var user = CreateUser();

        Assert.Throws<InvalidOperationException>(() => user.CompleteMfaEnrollment(_now));
    }

    [Fact]
    public void IssuePasswordResetChallenge_then_TryConsume_with_the_matching_hash_succeeds_exactly_once()
    {
        var user = CreateUser();
        user.IssuePasswordResetChallenge("token-hash", _now, TimeSpan.FromMinutes(30));

        var firstAttempt = user.TryConsumePasswordResetChallenge("token-hash", _now.AddMinutes(1));
        var secondAttempt = user.TryConsumePasswordResetChallenge("token-hash", _now.AddMinutes(2));

        Assert.True(firstAttempt);
        Assert.False(secondAttempt);
    }

    [Fact]
    public void TryConsumePasswordResetChallenge_fails_for_a_non_matching_hash()
    {
        var user = CreateUser();
        user.IssuePasswordResetChallenge("token-hash", _now, TimeSpan.FromMinutes(30));

        var result = user.TryConsumePasswordResetChallenge("wrong-hash", _now.AddMinutes(1));

        Assert.False(result);
    }

    [Fact]
    public void TryConsumePasswordResetChallenge_fails_once_expired()
    {
        var user = CreateUser();
        user.IssuePasswordResetChallenge("token-hash", _now, TimeSpan.FromMinutes(30));

        var result = user.TryConsumePasswordResetChallenge("token-hash", _now.AddMinutes(31));

        Assert.False(result);
    }

    [Fact]
    public void IssuePasswordResetChallenge_called_again_invalidates_the_prior_outstanding_token()
    {
        // edge-cases.md, "Concurrent password-reset requests": "issuing a new reset token invalidates all prior outstanding tokens."
        var user = CreateUser();
        user.IssuePasswordResetChallenge("first-hash", _now, TimeSpan.FromMinutes(30));

        user.IssuePasswordResetChallenge("second-hash", _now, TimeSpan.FromMinutes(30));

        Assert.False(user.TryConsumePasswordResetChallenge("first-hash", _now.AddMinutes(1)));
        Assert.True(user.TryConsumePasswordResetChallenge("second-hash", _now.AddMinutes(1)));
    }
}
