using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Research.Domain.Common;

/// <summary>
/// A monetary amount, local to Research. No shared <c>Money</c> type exists in this codebase -
/// <c>UMS.Modules.Finance.Domain.Common.Money</c> and <c>UMS.Modules.Library.Domain.Common.Money</c>
/// each live inside their own module's Domain layer and ADR-0002 forbids Research reaching into
/// another module's Domain internals, so this is Research's own copy of that same shape.
///
/// <para>
/// Deliberately DIFFERENT from Finance's/Library's own copies in one respect: those two restrict
/// <see cref="Create"/> to a single supported currency (BDT) since neither module's requirement-spec
/// names a genuine multi-currency case yet. Research's does - design-decisions.md "Grant
/// Funding-Amount Currency Handling" / edge-cases.md "Grant Funding-Amount Currency for an
/// International Grant" explicitly names an international grantor funding a Grant in a foreign
/// currency as an expected, in-scope case - so this copy accepts any well-formed ISO-4217-shaped
/// (3 upper-case letters) currency code with NO FX/conversion layer, exactly as those decisions
/// require: "store Money exactly as-is (amount + currency), no automatic FX conversion."
/// </para>
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
        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(char.IsAsciiLetterUpper))
        {
            return Error.Validation("money.invalid_currency", $"'{currency}' is not a well-formed 3-letter currency code.");
        }

        return new Money(Math.Round(amount, 2, MidpointRounding.ToEven), normalizedCurrency);
    }

    /// <summary>For infrastructure/EF value-conversion round-tripping of an already-validated stored value only - never for constructing a NEW amount from user/caller input (use <see cref="Create"/> for that).</summary>
    public static Money FromStoredValue(decimal amount, string currency) => new(amount, currency);
}
