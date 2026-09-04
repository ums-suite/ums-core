using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace UMS.Shared.Authorization;

public sealed class LiveSessionAuthorizationHandler(
    IPermissionResolver permissionResolver,
    ILogger<LiveSessionAuthorizationHandler> logger) : AuthorizationHandler<LiveSessionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LiveSessionRequirement requirement)
    {
        var subjectClaim = context.User.FindFirst(UmsClaimTypes.Subject)?.Value;
        var sessionClaim = context.User.FindFirst(UmsClaimTypes.SessionId)?.Value;

        if (!Guid.TryParse(subjectClaim, out var userId) || !Guid.TryParse(sessionClaim, out var sessionId))
        {
            logger.LogWarning("Authorization denied: token is missing a valid {Subject}/{SessionId} claim.", UmsClaimTypes.Subject, UmsClaimTypes.SessionId);
            return;
        }

        var outcome = await permissionResolver.CheckLivenessAsync(userId, sessionId).ConfigureAwait(false);
        if (outcome == PermissionCheckOutcome.Granted)
        {
            context.Succeed(requirement);
            return;
        }

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Live-session check denied for user {UserId} session {SessionId}: {Outcome}", userId, sessionId, outcome);
        }
    }
}
