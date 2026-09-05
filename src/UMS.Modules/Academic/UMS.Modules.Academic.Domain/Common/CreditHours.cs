using System.Globalization;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Domain.Common;

/// <summary>
/// A Course's credit-hour weight (ums-conventions.md, Domain Modeling: "any primitive carrying a
/// domain meaning, a validation rule, or a unit gets wrapped in a value object"). Module-local -
/// no shared platform-wide equivalent exists in <c>UMS.Shared.Domain</c> for this concept.
/// Bounded to a sane range (requirement-spec.md academic §2 Course, §4 credit-limit gate) so a
/// caller can never persist a nonsensical value like a 0 or negative credit course.
/// </summary>
public sealed record CreditHours
{
    private const int MaxValue = 12;

    private CreditHours(int value)
    {
        Value = value;
    }

    public int Value { get; }

    public static Result<CreditHours> Create(int value) =>
        value is < 1 or > MaxValue
            ? Error.Validation("credit_hours.out_of_range", $"CreditHours must be between 1 and {MaxValue}.")
            : new CreditHours(value);

    public override string ToString() => Value.ToString(CultureInfo.InvariantCulture);
}
