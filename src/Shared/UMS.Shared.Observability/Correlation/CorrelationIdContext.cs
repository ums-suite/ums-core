using Microsoft.AspNetCore.Http;

namespace UMS.Shared.Observability.Correlation;

/// <summary>
/// Single place that owns how a correlation id is read/stored on an <see cref="HttpContext"/>,
/// so <see cref="CorrelationIdMiddleware"/> (which sets it) and anything downstream that needs it
/// (e.g. UMS.Shared.ErrorHandling's exception handler) agree on the same key without a hard
/// dependency on the middleware type itself.
/// </summary>
public static class CorrelationIdContext
{
    public const string HeaderName = "X-Correlation-Id";

    private const string ItemKey = "UMS.CorrelationId";

    public static string GetOrCreate(HttpContext httpContext)
    {
        if (httpContext.Items.TryGetValue(ItemKey, out var existing) && existing is string correlationId)
        {
            return correlationId;
        }

        correlationId = httpContext.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : Guid.NewGuid().ToString("n");

        httpContext.Items[ItemKey] = correlationId;
        return correlationId;
    }
}
