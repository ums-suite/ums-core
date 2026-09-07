using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Domain.Common;

/// <summary>
/// A monetary amount. requirement-spec.md finance §9 Open Question: "no BRD section specifies
/// currency handling beyond an implicit single (BDT) currency" - <see cref="Create"/> enforces
/// exactly that single currency for this pass rather than silently accepting anything, so a future
/// multi-currency decision (flagged for Alumni's donation flow) has one obvious validation gate to
/// widen, not a scattered assumption to hunt down.
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
            return Error.Validation("money.unsupported_currency", $"Currency '{normalizedCurrency}' is not supported - only {DefaultCurrency} in this pass (requirement-spec.md §9 open question).");
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
