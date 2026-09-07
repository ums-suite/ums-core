using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.UnitTests.Common;

/// <summary>requirement-spec.md §9 Open Question: single (BDT) currency this pass - <see cref="Money.Create"/> is the one validation gate that enforces it.</summary>
public sealed class MoneyTests
{
    [Fact]
    public void A_negative_amount_is_rejected()
    {
        var result = Money.Create(-1m);

        Assert.True(result.IsFailure);
        Assert.Equal("money.negative_amount", result.Error!.Code);
    }

    [Fact]
    public void A_currency_other_than_BDT_is_rejected()
    {
        var result = Money.Create(100m, "USD");

        Assert.True(result.IsFailure);
        Assert.Equal("money.unsupported_currency", result.Error!.Code);
    }

    [Fact]
    public void Omitting_currency_defaults_to_BDT()
    {
        var result = Money.Create(100m);

        Assert.True(result.IsSuccess);
        Assert.Equal("BDT", result.Value.Currency);
    }

    [Fact]
    public void An_amount_is_rounded_to_two_decimal_places()
    {
        var result = Money.Create(100.005m);

        Assert.True(result.IsSuccess);
        Assert.Equal(100.00m, result.Value.Amount);
    }

    [Fact]
    public void Zero_is_a_valid_amount()
    {
        var result = Money.Create(0m);

        Assert.True(result.IsSuccess);
    }
}
