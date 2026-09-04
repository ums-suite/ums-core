using Microsoft.Extensions.Options;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Application.Auth;

/// <summary>
/// IDN-6: refresh token rotation with single-use reuse detection (requirement-spec.md identity
/// §2/§4/§6; edge-cases.md, "Same-device concurrent refresh (two tabs)"). All of the actual
/// reuse-vs-compromise decision logic lives in <see cref="Session.EvaluateRefreshAttempt"/> - this
/// service only wires the token-hashing/DB-lookup/cache-invalidation plumbing around it.
/// </summary>
public sealed class TokenRefreshService(
    ISessionRepository sessions,
    IUserRepository users,
    IRoleRepository roles,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IAuthzCache authzCache,
    IClock clock,
    IOptions<IdentityTokenOptions> tokenOptions)
{
    public async Task<Result<TokenPairResult>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        if (!tokenService.TryExtractSessionId(request.RefreshToken, out var sessionId))
        {
            return Error.Unauthorized("auth.invalid_refresh_token", "Refresh token is malformed.");
        }

        var session = await sessions.GetByIdAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Error.Unauthorized("auth.invalid_refresh_token", "Refresh token does not match any Session.");
        }

        var now = clock.UtcNow;
        var options = tokenOptions.Value;
        var presentedHash = tokenService.HashRefreshToken(request.RefreshToken);
        var decision = session.EvaluateRefreshAttempt(presentedHash, now, options.RefreshReuseGraceWindow);

        switch (decision)
        {
            case RefreshAttemptDecision.Rejected:
                return Error.Unauthorized("auth.refresh_rejected", "Session is revoked or the refresh token has expired.");

            case RefreshAttemptDecision.CompromiseDetected:
                // Identity §4: "revokes the entire Session chain, not just that token" - "chain"
                // here means this one Session (one device's rotation history), never every
                // Session the User holds - a different device's Session is untouched.
                session.Revoke("refresh_token_reuse_detected", now);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await authzCache.RevokeSessionAsync(sessionId, options.AccessTokenLifetime, cancellationToken).ConfigureAwait(false);
                return Error.Unauthorized("auth.refresh_reuse_detected", "This refresh token was already used - the Session has been revoked.");

            case RefreshAttemptDecision.RotateNormally:
            case RefreshAttemptDecision.GraceReuse:
                break;

            default:
                throw new InvalidOperationException($"Unhandled {nameof(RefreshAttemptDecision)}: {decision}");
        }

        var user = await users.GetByIdAsync(session.UserId, cancellationToken).ConfigureAwait(false);
        if (user is null)
        {
            return Error.Unauthorized("auth.invalid_refresh_token", "The User behind this Session no longer exists.");
        }

        if (user.Status != Domain.Users.UserStatus.Active)
        {
            return Error.Forbidden("auth.user_inactive", "This User account is not active.");
        }

        var newRefreshToken = tokenService.IssueRefreshToken(sessionId, now);
        session.Rotate(newRefreshToken.Hash, newRefreshToken.ExpiresAt, now);

        var activeRoles = await AuthenticationService.GetActiveRolesAsync(user, roles, cancellationToken).ConfigureAwait(false);
        var accessToken = tokenService.IssueAccessToken(user.Id, sessionId, activeRoles.Select(r => r.Name).ToList(), now);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new TokenPairResult(accessToken.Value, accessToken.ExpiresAt, newRefreshToken.PlaintextValue, newRefreshToken.ExpiresAt, sessionId.Value);
    }
}
