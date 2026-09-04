using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace UMS.Shared.Authorization;

/// <summary>
/// Evaluates a <see cref="PermissionRequirement"/> against <see cref="IPermissionResolver"/> -
/// the user-status/session-revocation/permission steps of the fixed validation order (identity
/// §2). Authentication and token validity (the first two steps) already happened by the time an
/// <see cref="IAuthorizationHandler"/> runs at all - ASP.NET Core's JwtBearer middleware rejects
/// an invalid/expired token before authorization is ever evaluated.
/// </summary>
public sealed class PermissionAuthorizationHandler(
    IPermissionResolver permissionResolver,
    ILogger<PermissionAuthorizationHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var subjectClaim = context.User.FindFirst(UmsClaimTypes.Subject)?.Value;
        var sessionClaim = context.User.FindFirst(UmsClaimTypes.SessionId)?.Value;

        if (!Guid.TryParse(subjectClaim, out var userId) || !Guid.TryParse(sessionClaim, out var sessionId))
        {
            logger.LogWarning("Authorization denied: token is missing a valid {Subject}/{SessionId} claim.", UmsClaimTypes.Subject, UmsClaimTypes.SessionId);
            return;
        }

        var outcome = await permissionResolver
            .CheckAsync(userId, sessionId, requirement.Permission)
            .ConfigureAwait(false);

        if (outcome == PermissionCheckOutcome.Granted)
        {
            context.Succeed(requirement);
            return;
        }

        // A previously-issued, still-unexpired JWT is never sufficient by itself (identity §4) -
        // every one of these non-Granted outcomes denies the request even though the token itself
        // is cryptographically valid and unexpired.
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Permission check denied for user {UserId} session {SessionId} permission {Permission}: {Outcome}",
                userId,
                sessionId,
                requirement.Permission,
                outcome);
        }
    }
}
