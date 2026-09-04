using Microsoft.AspNetCore.Http;
using UMS.Modules.Organization.Application.Common;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Organization.Api;

/// <summary>Builds the one <see cref="AuditContext"/> every mutation endpoint hands to its Application service - extracted once here rather than re-derived at each of Organization's ~15 mutation call sites.</summary>
internal static class HttpContextExtensions
{
    public static AuditContext GetAuditContext(this HttpContext httpContext) => new(
        httpContext.User.GetUserId(),
        httpContext.Connection.RemoteIpAddress?.ToString(),
        CorrelationIdContext.GetOrCreate(httpContext));
}
