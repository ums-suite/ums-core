using System.Security.Claims;
using UMS.Shared.Authorization;

namespace UMS.Modules.Faculty.Api;

/// <summary>
/// An ad-hoc permission check for the handful of endpoints whose authorization can't be expressed
/// as one static <c>RequirePermission</c> policy - e.g. LeaveRequest reads, which the owning
/// FacultyMember and any approver may both see, or ResearchProfile writes, which the owning
/// FacultyMember and HR may both perform (requirement-spec.md faculty §6). Calls the same
/// <see cref="IPermissionResolver"/> the static policy path uses, so the outcome (including the
/// user-status/session-revocation steps) is identical either way.
/// </summary>
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
