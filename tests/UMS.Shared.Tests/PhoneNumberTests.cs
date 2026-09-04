using UMS.Shared.Domain;

namespace UMS.Shared.Tests;

public class PhoneNumberTests
{
    [Fact]
    public void Create_normalizes_a_Bangladesh_local_number_to_E164()
    {
        var result = PhoneNumber.Create("01712345678");

        Assert.True(result.IsSuccess);
        Assert.Equal("+8801712345678", result.Value.Value);
    }

    [Fact]
    public void Create_accepts_an_already_normalized_Bangladesh_E164_number()
    {
        var result = PhoneNumber.Create("+8801712345678");

        Assert.True(result.IsSuccess);
        Assert.Equal("+8801712345678", result.Value.Value);
    }

    [Fact]
    public void Create_accepts_a_general_international_E164_number()
    {
        var result = PhoneNumber.Create("+14155552671");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("01212345678")] // invalid Bangladesh operator prefix (012)
    [InlineData("not-a-number")]
    public void Create_rejects_invalid_input(string input)
    {
        var result = PhoneNumber.Create(input);

        Assert.False(result.IsSuccess);
    }
}
