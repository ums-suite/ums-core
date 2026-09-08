using Microsoft.AspNetCore.Http;
using UMS.Shared.Observability.Correlation;

namespace UMS.Modules.Library.Api;

internal static class HttpContextExtensions
{
    public static string GetCorrelationId(this HttpContext httpContext) => CorrelationIdContext.GetOrCreate(httpContext);
}
