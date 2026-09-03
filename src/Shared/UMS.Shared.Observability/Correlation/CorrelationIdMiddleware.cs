using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace UMS.Shared.Observability.Correlation;

/// <summary>
/// Resolves (or creates) the request's correlation id, echoes it back on the response, and pushes
/// it onto Serilog's <see cref="LogContext"/> for the lifetime of the request - every structured
/// log line for this request carries it without each call site passing it explicitly
/// (ums-conventions.md, Observability: "every module's structured logs/traces carry
/// correlationId").
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationId = CorrelationIdContext.GetOrCreate(httpContext);

        httpContext.Response.OnStarting(() =>
        {
            httpContext.Response.Headers[CorrelationIdContext.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("correlationId", correlationId))
        {
            await next(httpContext);
        }
    }
}
