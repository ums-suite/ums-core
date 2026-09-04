using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Events;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Auth;

/// <summary>
/// IDN-5: login - identifier resolution, password verification, `Session` creation, JWT
/// access + rotating refresh token issuance (requirement-spec.md identity §2/§3/§6). IDN-17's
/// failed-login throttling/lockout gates this before password verification even runs for an
/// already-locked-out account; IDN-11's MFA gate runs after a successful password check, deferring
/// token issuance to <see cref="CompleteLoginAsync"/> when an active Role requires MFA.
/// </summary>
public sealed class AuthenticationService(
    IUserRepository users,
    IRoleRepository roles,
    ISessionRepository sessions,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IFailedLoginAttemptTracker failedLoginAttempts,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IClock clock,
    IOptions<IdentityLockoutOptions> lockoutOptions)
{
    public async Task<Result<LoginOutcome>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdentifierAsync(request.Identifier, cancellationToken).ConfigureAwait(false);

        // requirement-spec.md identity §8 "Password reset requested for a locked-out account" -
        // lockout is checked, and fails the same way, before password verification even runs: no
        // point spending an Argon2 verify on an account that cannot log in regardless of the
        // result, and it keeps the locked-account response uniform regardless of whether the
        // presented password happens to be correct.
        if (user is not null && user.LockedOutAt is not null)
        {
            return Error.Forbidden("auth.account_locked", "This account is locked due to repeated failed login attempts. Reset your password to regain access.");
        }

        // Same generic failure for "unknown identifier" and "wrong password" - never reveal which
        // half was wrong (requirement-spec.md identity §5 Security NFR).
        if (user is null || !passwordHasher.VerifyPassword(request.Password, user.Credential.PasswordHash))
        {
            // No aggregate was necessarily loaded (unknown identifier), so this event is recorded
            // directly rather than raised through User - see UserLoginFailed's own remarks.
            domainEvents.Enqueue(new UserLoginFailed(request.Identifier, "invalid_credentials", clock.UtcNow));

            var failureCount = await failedLoginAttempts.RegisterFailureAsync(request.Identifier, cancellationToken).ConfigureAwait(false);
            if (user is not null && failureCount >= lockoutOptions.Value.MaxFailedAttempts)
            {
                user.LockOut(clock.UtcNow);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Error.Unauthorized("auth.invalid_credentials", "Invalid identifier or password.");
        }

        if (user.Status != UserStatus.Active)
        {
            return Error.Forbidden("auth.user_inactive", "This User account is not active.");
        }

        await failedLoginAttempts.ResetAsync(request.Identifier, cancellationToken).ConfigureAwait(false);

        var activeRoles = await GetActiveRolesAsync(user, cancellationToken).ConfigureAwait(false);

        // IDN-16: §4 "MFA cannot be silently downgraded" - a User holding a Role that requires MFA
        // never receives a full token pair from LoginAsync directly, enrolled or not; §8's edge case
        // is explicit that the Role assignment itself already succeeded, so this is a "complete MFA
        // to activate this role" state, never a denial.
        if (activeRoles.Any(r => r.RequiresMfa))
        {
            var now = clock.UtcNow;
            var challenge = tokenService.IssueMfaChallengeToken(user.Id, now);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new LoginRequiresMfa(challenge.Value, challenge.ExpiresAt, user.Mfa.IsEnrolled);
        }

        var tokens = await CompleteLoginAsync(user, activeRoles, request.UserAgent, request.IpAddress, cancellationToken).ConfigureAwait(false);
        return new LoginSucceeded(tokens);
    }

    /// <summary>
    /// The shared "actually issue a Session + token pair" tail end of login - called directly by
    /// <see cref="LoginAsync"/> when no active Role requires MFA, and by
    /// <see cref="MfaEnrollmentService"/> once a mid-login MFA challenge has been satisfied
    /// (<c>POST /auth/mfa/verify</c>, IDN-11).
    /// </summary>
    public async Task<TokenPairResult> CompleteLoginAsync(User user, IReadOnlyCollection<Role> activeRoles, string? userAgent, string? ipAddress, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var sessionId = SessionId.New();

        var refreshToken = tokenService.IssueRefreshToken(sessionId, now);
        var session = Session.Open(sessionId, user.Id, refreshToken.Hash, refreshToken.ExpiresAt, userAgent, ipAddress, now);
        sessions.Add(session);

        var accessToken = tokenService.IssueAccessToken(user.Id, sessionId, activeRoles.Select(r => r.Name).ToList(), now);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TokenPairResult(accessToken.Value, accessToken.ExpiresAt, refreshToken.PlaintextValue, refreshToken.ExpiresAt, sessionId.Value);
    }

    public Task<IReadOnlyCollection<Role>> GetActiveRolesAsync(User user, CancellationToken cancellationToken) =>
        GetActiveRolesAsync(user, roles, cancellationToken);

    internal static async Task<IReadOnlyCollection<Role>> GetActiveRolesAsync(User user, IRoleRepository roles, CancellationToken cancellationToken)
    {
        var activeRoleIds = user.RoleAssignments.Where(a => a.IsActive).Select(a => a.RoleId).Distinct().ToList();
        if (activeRoleIds.Count == 0)
        {
            return [];
        }

        return await roles.GetByIdsAsync(activeRoleIds, cancellationToken).ConfigureAwait(false);
    }
}
