using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using UMS.Shared.Observability.Correlation;

namespace UMS.Shared.ErrorHandling;

/// <summary>
/// The one place an unhandled exception is caught (ums-conventions.md: "exceptions stay reserved
/// for genuine infrastructure failures, caught exactly once by the global handler"). Domain/business
/// errors never reach this type - they're returned as a <see cref="Results.Result"/> and mapped to
/// a response at the endpoint, never thrown.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdContext.GetOrCreate(httpContext);

        // Logged exactly once, at Error level, tagged with correlationId (ums-conventions.md).
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path} [correlationId={CorrelationId}]",
            httpContext.Request.Method,
            httpContext.Request.Path,
            correlationId);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://ums-suite.internal/errors/unexpected",
        };
        problemDetails.Extensions["code"] = "UNEXPECTED_ERROR";
        problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
