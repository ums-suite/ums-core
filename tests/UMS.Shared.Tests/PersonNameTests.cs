using UMS.Shared.Domain;

namespace UMS.Shared.Tests;

public class PersonNameTests
{
    [Fact]
    public void Create_builds_a_display_name_from_given_and_family_name()
    {
        var result = PersonName.Create("Jane", "Doe");

        Assert.True(result.IsSuccess);
        Assert.Equal("Jane Doe", result.Value.DisplayName);
    }

    [Fact]
    public void Create_accepts_an_optional_Bengali_rendering()
    {
        var result = PersonName.Create("Jane", "Doe", "জেন", "ডো");

        Assert.True(result.IsSuccess);
        Assert.Equal("জেন", result.Value.GivenNameBn);
    }

    [Theory]
    [InlineData(null, "Doe")]
    [InlineData("Jane", null)]
    [InlineData("", "Doe")]
    public void Create_rejects_a_missing_given_or_family_name(string? givenName, string? familyName)
    {
        var result = PersonName.Create(givenName, familyName);

        Assert.False(result.IsSuccess);
    }
}
