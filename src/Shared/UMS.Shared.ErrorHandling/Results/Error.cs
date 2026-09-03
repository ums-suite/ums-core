namespace UMS.Shared.ErrorHandling.Results;

/// <summary>
/// Error severity, used by <see cref="GlobalExceptionHandler"/> to pick the correct HTTP status
/// code when translating a domain <see cref="Error"/> into a response.
/// </summary>
public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Failure,
}

/// <summary>
/// A machine-readable domain/business error. <see cref="Code"/> is stable and intended for
/// client-side branching (ums-conventions.md: "a consistent envelope ... so the generated
/// TypeScript client ... never special-cases one module's response shape against another's").
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type = ErrorType.Failure)
{
    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);
}
