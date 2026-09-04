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
/// access + rotating refresh token issuance (requirement-spec.md identity §2/§3/§6).
/// Failed-login throttling/lockout (IDN-17) is not yet wired in - see release/DEVELOPMENT_PLAN.md
/// Flow #4 status for that ticket's deferral.
/// </summary>
public sealed class AuthenticationService(
    IUserRepository users,
    IRoleRepository roles,
    ISessionRepository sessions,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IClock clock,
    IOptions<IdentityTokenOptions> tokenOptions)
{
    public async Task<Result<TokenPairResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdentifierAsync(request.Identifier, cancellationToken).ConfigureAwait(false);

        // Same generic failure for "unknown identifier" and "wrong password" - never reveal which
        // half was wrong (requirement-spec.md identity §5 Security NFR).
        if (user is null || !passwordHasher.VerifyPassword(request.Password, user.Credential.PasswordHash))
        {
            // No aggregate was necessarily loaded (unknown identifier), so this event is recorded
            // directly rather than raised through User - see UserLoginFailed's own remarks. Feeds
            // IDN-17's lockout counter and Audit once those consumers exist; the outbox row is
            // written today regardless, via the same transactional path as every other event.
            domainEvents.Enqueue(new UserLoginFailed(request.Identifier, "invalid_credentials", clock.UtcNow));
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Error.Unauthorized("auth.invalid_credentials", "Invalid identifier or password.");
        }

        if (user.Status != UserStatus.Active)
        {
            return Error.Forbidden("auth.user_inactive", "This User account is not active.");
        }

        var now = clock.UtcNow;
        var options = tokenOptions.Value;
        var sessionId = SessionId.New();

        var refreshToken = tokenService.IssueRefreshToken(sessionId, now);
        var session = Session.Open(sessionId, user.Id, refreshToken.Hash, refreshToken.ExpiresAt, request.UserAgent, request.IpAddress, now);
        sessions.Add(session);

        var roleNames = await GetActiveRoleNamesAsync(user, cancellationToken).ConfigureAwait(false);
        var accessToken = tokenService.IssueAccessToken(user.Id, sessionId, roleNames, now);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TokenPairResult(accessToken.Value, accessToken.ExpiresAt, refreshToken.PlaintextValue, refreshToken.ExpiresAt, sessionId.Value);
    }

    internal static async Task<IReadOnlyCollection<string>> GetActiveRoleNamesAsync(User user, IRoleRepository roles, CancellationToken cancellationToken)
    {
        var activeRoleIds = user.RoleAssignments.Where(a => a.IsActive).Select(a => a.RoleId).Distinct().ToList();
        if (activeRoleIds.Count == 0)
        {
            return [];
        }

        var loaded = await roles.GetByIdsAsync(activeRoleIds, cancellationToken).ConfigureAwait(false);
        return loaded.Select(r => r.Name).ToList();
    }

    private Task<IReadOnlyCollection<string>> GetActiveRoleNamesAsync(User user, CancellationToken cancellationToken) =>
        GetActiveRoleNamesAsync(user, roles, cancellationToken);
}
