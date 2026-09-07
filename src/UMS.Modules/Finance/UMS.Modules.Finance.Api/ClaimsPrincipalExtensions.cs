using System.Security.Claims;
using UMS.Shared.Authorization;

namespace UMS.Modules.Finance.Api;

internal static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirst(UmsClaimTypes.Subject)?.Value ?? throw new InvalidOperationException("Missing 'sub' claim."));
}
