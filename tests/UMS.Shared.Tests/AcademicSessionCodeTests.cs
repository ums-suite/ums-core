using UMS.Shared.Domain;

namespace UMS.Shared.Tests;

public sealed class AcademicSessionCodeTests
{
    [Theory]
    [InlineData("2025-2026", true)]
    [InlineData("2025-2027", false)]
    [InlineData("2025", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Create_requires_a_YYYY_YYYY_format_with_consecutive_years(string? value, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, AcademicSessionCode.Create(value).IsSuccess);
    }
}
