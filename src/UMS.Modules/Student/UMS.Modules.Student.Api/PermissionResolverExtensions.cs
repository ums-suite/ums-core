using System.Security.Claims;
using UMS.Shared.Authorization;

namespace UMS.Modules.Student.Api;

/// <summary>An ad-hoc permission check for the one endpoint whose authorization can't be expressed as a single static <c>RequirePermission</c> policy - <c>GET /students/requests/{id}</c>, which the owning Student and any authorized reviewer may both see (requirement-spec.md §6). Mirrors Faculty's/Academic's own identically-named extension exactly.</summary>
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
