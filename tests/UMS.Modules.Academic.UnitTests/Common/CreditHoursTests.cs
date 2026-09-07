using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.UnitTests.Common;

public sealed class CreditHoursTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(12, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(13, false)]
    public void Create_bounds_the_value_between_1_and_12(int value, bool expectedSuccess)
    {
        var result = CreditHours.Create(value);
        Assert.Equal(expectedSuccess, result.IsSuccess);
    }
}
