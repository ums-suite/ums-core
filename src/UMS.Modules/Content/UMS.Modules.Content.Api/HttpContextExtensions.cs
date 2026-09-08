using Microsoft.AspNetCore.Http;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Content.Api;

internal static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext httpContext) => CorrelationIdContext.GetOrCreate(httpContext);

    /// <summary>
    /// ADR-0011: resolves the caller's preferred language from the standard `Accept-Language`
    /// header - a bare `"bn"` (any position, any q-value) is enough to prefer Bengali; anything
    /// else (missing header, `"en"`, unrecognized) falls back to English (represented as
    /// <see langword="null"/> here - see every service's own <c>ToDto</c>).
    /// </summary>
    public static string? GetPreferredLanguage(this HttpContext httpContext)
    {
        var header = httpContext.Request.Headers.AcceptLanguage.ToString();
        return !string.IsNullOrWhiteSpace(header) && header.Contains("bn", StringComparison.OrdinalIgnoreCase) ? "bn" : null;
    }

    /// <summary>The caller's own user id, or <see langword="null"/> for an anonymous/public request (CNT-3: audience scoping treats these differently, never the same as "no restriction").</summary>
    public static Guid? GetOptionalUserId(this HttpContext httpContext)
    {
        var claim = httpContext.User.FindFirst(UMS.Shared.Authorization.UmsClaimTypes.Subject)?.Value;
        return claim is not null && Guid.TryParse(claim, out var userId) ? userId : null;
    }
}
