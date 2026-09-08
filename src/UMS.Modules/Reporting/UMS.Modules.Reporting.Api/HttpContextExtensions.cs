using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Authorization;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Reporting.Api;

internal static class HttpContextExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirst(UmsClaimTypes.Subject)?.Value ?? throw new InvalidOperationException("Missing 'sub' claim."));

    public static string GetCorrelationId(this HttpContext httpContext) => CorrelationIdContext.GetOrCreate(httpContext);

    /// <summary>Bundles the caller-identity fields RPT-16's Audit calls need - mirrors every other module's own <c>GetAuditContext</c> exactly (see e.g. Finance's own).</summary>
    public static AuditContext GetAuditContext(this HttpContext httpContext) => new(
        httpContext.User.GetUserId(),
        httpContext.Connection.RemoteIpAddress?.ToString(),
        CorrelationIdContext.GetOrCreate(httpContext));
}
