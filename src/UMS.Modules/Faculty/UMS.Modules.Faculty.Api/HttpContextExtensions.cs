using Microsoft.AspNetCore.Http;
using UMS.Modules.Faculty.Application.Common;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Faculty.Api;

internal static class HttpContextExtensions
{
    public static AuditContext GetAuditContext(this HttpContext httpContext) => new(
        httpContext.User.GetUserId(),
        httpContext.Connection.RemoteIpAddress?.ToString(),
        CorrelationIdContext.GetOrCreate(httpContext));
}
