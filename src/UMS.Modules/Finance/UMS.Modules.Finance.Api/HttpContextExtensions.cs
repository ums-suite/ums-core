using Microsoft.AspNetCore.Http;
using UMS.Modules.Finance.Application.Common;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Finance.Api;

internal static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext httpContext) => CorrelationIdContext.GetOrCreate(httpContext);

    /// <summary>FIN-11: bundles the caller-identity fields RefundService's own Audit call needs - mirrors every other module's own <c>GetAuditContext</c> exactly.</summary>
    public static AuditContext GetAuditContext(this HttpContext httpContext) => new(
        httpContext.User.GetUserId(),
        httpContext.Connection.RemoteIpAddress?.ToString(),
        CorrelationIdContext.GetOrCreate(httpContext));
}
