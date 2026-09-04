using System.Text.RegularExpressions;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Domain;

/// <summary>
/// A validated phone number, Bangladesh-local or E.164 international
/// (ums-conventions.md, Domain Modeling: "PhoneNumber (Bangladesh + international formats)").
/// Stored normalized to E.164 (<c>+8801XXXXXXXXX</c> for an 11-digit Bangladesh local number)
/// so equality/lookup never has to reconcile two representations of the same number.
/// </summary>
public sealed partial record PhoneNumber
{
    private PhoneNumber(string value)
    {
        Value = value;
    }

    /// <summary>The normalized E.164 representation, e.g. <c>+8801712345678</c>.</summary>
    public string Value { get; }

    public static Result<PhoneNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Error.Validation("phone_number.required", "Phone number is required.");
        }

        var trimmed = value.Trim();

        // Bangladesh local format: 01XXXXXXXXX (11 digits, starts 013-019) - normalize to +880...
        if (BangladeshLocalPattern().IsMatch(trimmed))
        {
            return new PhoneNumber("+880" + trimmed[1..]);
        }

        // Already-normalized Bangladesh E.164 form.
        if (BangladeshE164Pattern().IsMatch(trimmed))
        {
            return new PhoneNumber(trimmed);
        }

        // General E.164 international fallback: + followed by 8-15 digits.
        if (E164Pattern().IsMatch(trimmed))
        {
            return new PhoneNumber(trimmed);
        }

        return Error.Validation("phone_number.invalid_format", "Phone number is not a valid Bangladesh or international number.");
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^01[3-9]\d{8}$", RegexOptions.Compiled)]
    private static partial Regex BangladeshLocalPattern();

    [GeneratedRegex(@"^\+8801[3-9]\d{8}$", RegexOptions.Compiled)]
    private static partial Regex BangladeshE164Pattern();

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.Compiled)]
    private static partial Regex E164Pattern();
}
