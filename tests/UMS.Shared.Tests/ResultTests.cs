using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Tests;

public class ResultTests
{
    [Fact]
    public void Success_result_carries_no_error()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_result_carries_the_given_error()
    {
        var error = Error.Validation("STUDENT_ID_REQUIRED", "A student id is required.");

        var result = Result.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Generic_success_result_exposes_its_value()
    {
        Result<int> result = Result.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Accessing_value_of_a_failed_generic_result_throws()
    {
        Result<int> result = Error.NotFound("STUDENT_NOT_FOUND", "No such student.");

        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Implicit_conversion_from_value_produces_a_success_result()
    {
        Result<string> result = "S-100";

        Assert.True(result.IsSuccess);
        Assert.Equal("S-100", result.Value);
    }

    [Fact]
    public void Match_invokes_the_branch_matching_the_result_outcome()
    {
        Result<int> success = Result.Success(1);
        Result<int> failure = Error.Conflict("DUPLICATE", "Already exists.");

        Assert.Equal("ok", success.Match(_ => "ok", _ => "error"));
        Assert.Equal("error", failure.Match(_ => "ok", _ => "error"));
    }
}
