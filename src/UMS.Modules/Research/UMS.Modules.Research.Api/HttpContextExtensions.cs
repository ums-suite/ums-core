using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using UMS.Modules.Research.Application.Common;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Research.Api;

internal static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext httpContext) => CorrelationIdContext.GetOrCreate(httpContext);

    public static AuditContext GetAuditContext(this HttpContext httpContext) => new(
        httpContext.User.GetUserId(),
        httpContext.Connection.RemoteIpAddress?.ToString(),
        httpContext.GetCorrelationId());

    /// <summary>
    /// requirement-spec.md §5 Caching: the public showcase's own `Cache-Control`/`ETag` treatment
    /// (design-decisions.md's visibility-filtering decision) - mirrors Content's public-endpoint
    /// caching posture. A weak ETag computed from the serialized response body; a matching
    /// `If-None-Match` short-circuits to a bodyless 304 rather than re-serializing/re-transferring
    /// an unchanged page. <paramref name="maxAgeSeconds"/> is deliberately short (a few minutes) -
    /// long enough to be genuinely cacheable at the CDN per ums-conventions.md, short enough that a
    /// newly-lifted embargo or a newly-flagged-private Grant/Publication does not stay visible long
    /// past its own state change.
    /// </summary>
    public static IResult PublicCacheableJson<T>(this HttpContext httpContext, T payload, int maxAgeSeconds = 300)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var etag = $"\"{Convert.ToHexString(hash)}\"";

        httpContext.Response.Headers.CacheControl = $"public, max-age={maxAgeSeconds}";
        httpContext.Response.Headers.ETag = etag;

        if (httpContext.Request.Headers.TryGetValue("If-None-Match", out StringValues ifNoneMatch) && ifNoneMatch.ToString() == etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Text(json, "application/json");
    }
}
