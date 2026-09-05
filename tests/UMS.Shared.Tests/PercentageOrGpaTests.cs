using UMS.Shared.Domain;

namespace UMS.Shared.Tests;

public sealed class PercentageOrGpaTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(-1, false)]
    [InlineData(101, false)]
    public void CreatePercentage_bounds_between_0_and_100(decimal value, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, PercentageOrGpa.CreatePercentage(value).IsSuccess);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4.0, true)]
    [InlineData(-0.1, false)]
    [InlineData(6.0, false)]
    public void CreateGpa_bounds_between_0_and_MaxGpaScale(decimal value, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, PercentageOrGpa.CreateGpa(value).IsSuccess);
    }
}
