using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Tests;

public class EmailTests
{
    [Theory]
    [InlineData("Jane.Doe@Example.EDU.BD")]
    [InlineData("jane.doe@example.edu.bd")]
    public void Create_normalizes_to_lowercase(string input)
    {
        var result = Email.Create(input);

        Assert.True(result.IsSuccess);
        Assert.Equal("jane.doe@example.edu.bd", result.Value.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("not-an-email")]
    [InlineData("missing-domain@")]
    [InlineData("@missing-local.com")]
    public void Create_rejects_invalid_input(string? input)
    {
        var result = Email.Create(input);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public void Two_emails_differing_only_by_case_are_equal()
    {
        var first = Email.Create("Jane@Example.com").Value;
        var second = Email.Create("jane@example.com").Value;

        Assert.Equal(first, second);
    }
}
