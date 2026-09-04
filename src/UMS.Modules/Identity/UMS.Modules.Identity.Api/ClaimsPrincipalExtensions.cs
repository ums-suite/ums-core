using System.Security.Claims;
using UMS.Shared.Authorization;

namespace UMS.Modules.Identity.Api;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirst(UmsClaimTypes.Subject)?.Value ?? throw new InvalidOperationException("Missing 'sub' claim."));

    public static Guid GetSessionId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirst(UmsClaimTypes.SessionId)?.Value ?? throw new InvalidOperationException("Missing 'sid' claim."));

    /// <summary>IDN-11: true for a mid-login MFA-challenge token (no real Session yet), false for an ordinary, already-live-session access token.</summary>
    public static bool IsMfaChallenge(this ClaimsPrincipal principal) =>
        principal.FindFirst(UmsClaimTypes.MfaChallenge) is not null;
}
