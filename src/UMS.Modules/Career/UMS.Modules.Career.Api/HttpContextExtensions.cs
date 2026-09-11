using Microsoft.AspNetCore.Http;
using UMS.Modules.Career.Application.Common;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Career.Api;

internal static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext httpContext) => CorrelationIdContext.GetOrCreate(httpContext);

    public static AuditContext GetAuditContext(this HttpContext httpContext) => new(
        httpContext.User.GetUserId(),
        httpContext.Connection.RemoteIpAddress?.ToString(),
        httpContext.GetCorrelationId());
}
