using Microsoft.AspNetCore.Http;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Observability.Correlation;

namespace UMS.Shared.ErrorHandling;

/// <summary>
/// Maps a failed <see cref="Result"/> to the same ProblemDetails envelope shape
/// <see cref="GlobalExceptionHandler"/> uses for unhandled exceptions, so a module's minimal-API
/// endpoint never hand-rolls its own error response.
/// </summary>
public static class ResultEndpointExtensions
{
    public static IResult ToProblemResult(this Error error, HttpContext httpContext)
    {
        var correlationId = CorrelationIdContext.GetOrCreate(httpContext);
        var statusCode = ToStatusCode(error.Type);

        return Microsoft.AspNetCore.Http.Results.Problem(
            statusCode: statusCode,
            title: error.Message,
            type: $"https://ums-suite.internal/errors/{error.Type.ToString().ToLowerInvariant()}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = error.Code,
                ["correlationId"] = correlationId,
            });
    }

    private static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Failure => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };
}
