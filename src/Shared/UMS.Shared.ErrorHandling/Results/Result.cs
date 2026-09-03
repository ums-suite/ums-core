namespace UMS.Shared.ErrorHandling.Results;

/// <summary>
/// The platform-wide Result pattern (ums-conventions.md, Error Handling & Response Consistency:
/// "Business/domain errors use a Result-pattern return, not exceptions"). Every application
/// service that can fail for a domain reason returns a <see cref="Result"/>/<see cref="Result{T}"/>
/// instead of throwing - exceptions stay reserved for genuine infrastructure failures, caught
/// exactly once by <see cref="GlobalExceptionHandler"/>.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error is null)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public static Result Success() => new(true, null);

    public static Result Failure(Error error) => new(false, error);

    public static Result<T> Success<T>(T value) => new(value, true, null);

    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

/// <summary>A <see cref="Result"/> that carries a value on the success path.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, bool isSuccess, Error? error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The success value. Throws if accessed on a failed result - callers must check
    /// <see cref="Result.IsSuccess"/> (or use <see cref="Match"/>) first, the same discipline
    /// nullable-reference checking already forces everywhere else in this codebase.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure<T>(error);

    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(Error!);
}
