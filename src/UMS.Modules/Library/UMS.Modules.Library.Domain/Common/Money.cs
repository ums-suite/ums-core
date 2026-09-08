using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Common;

/// <summary>
/// A monetary amount, local to Library. No shared <c>Money</c> type exists in this codebase -
/// <c>UMS.Modules.Finance.Domain.Common.Money</c> lives inside Finance's own Domain layer and
/// ADR-0002 forbids Library reaching into another module's Domain internals, so this is Library's
/// own copy of Finance's exact shape (single currency, 2-decimal rounding, non-negative), not a
/// shared abstraction. <see cref="Fine.Amount"/> is a top-level aggregate's own field, so it can use
/// EF's <c>ComplexProperty</c> directly - only a Money field nested one level inside an
/// <c>OwnsMany</c> child needs the flattened-scalar-columns workaround (see
/// <see cref="Infrastructure.Persistence.Configurations.FineConfiguration"/>'s own remarks - not
/// needed here since Library has no such nested-Money shape).
/// </summary>
public readonly record struct Money
{
    public const string DefaultCurrency = "BDT";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public static Money Zero => new(0m, DefaultCurrency);

    public static Result<Money> Create(decimal amount, string? currency = null)
    {
        if (amount < 0)
        {
            return Error.Validation("money.negative_amount", "A monetary amount must not be negative.");
        }

        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? DefaultCurrency : currency.Trim().ToUpperInvariant();
        if (normalizedCurrency != DefaultCurrency)
        {
            return Error.Validation("money.unsupported_currency", $"Currency '{normalizedCurrency}' is not supported - only {DefaultCurrency} in this pass.");
        }

        return new Money(Math.Round(amount, 2, MidpointRounding.ToEven), normalizedCurrency);
    }

    /// <summary>For infrastructure/EF value-conversion round-tripping of an already-validated stored value only - never for constructing a NEW amount from user/caller input (use <see cref="Create"/> for that).</summary>
    public static Money FromStoredValue(decimal amount, string currency) => new(amount, currency);

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
        {
            throw new InvalidOperationException($"Cannot add {other.Currency} to a {Currency} amount.");
        }

        return new Money(Amount + other.Amount, Currency);
    }
}
