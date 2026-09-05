using System.Security.Claims;
using UMS.Shared.Authorization;

namespace UMS.Modules.Academic.Api;

/// <summary>Mirrors Faculty's own <c>PermissionResolverExtensions</c> exactly - an ad-hoc permission check for the one endpoint pair (ACD-14/ACD-15) whose authorization is "a named permission OR the owning Student themself", which can't be expressed as one static <c>RequirePermission</c> policy.</summary>
internal static class PermissionResolverExtensions
{
    public static async Task<bool> HasPermissionAsync(this IPermissionResolver resolver, ClaimsPrincipal principal, string permission, CancellationToken cancellationToken = default)
    {
        var sessionClaim = principal.FindFirst(UmsClaimTypes.SessionId)?.Value;
        if (!Guid.TryParse(sessionClaim, out var sessionId))
        {
            return false;
        }

        var outcome = await resolver.CheckAsync(principal.GetUserId(), sessionId, permission, cancellationToken).ConfigureAwait(false);
        return outcome == PermissionCheckOutcome.Granted;
    }
}
