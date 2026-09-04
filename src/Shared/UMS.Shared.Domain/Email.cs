using System.Text.RegularExpressions;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Domain;

/// <summary>
/// A validated email address (ums-conventions.md, Domain Modeling: "any primitive carrying a
/// domain meaning ... gets wrapped in a value object"). Scaffolded as part of Identity (release/
/// DEVELOPMENT_PLAN.md Flow #4, "Sequencing conflicts and decisions") since login-by-email and
/// password-reset delivery both need it; every later module reuses this instead of re-declaring
/// its own email type.
/// </summary>
public sealed partial record Email
{
    private const int MaxLength = 254;

    private Email(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Error.Validation("email.required", "Email is required.");
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > MaxLength)
        {
            return Error.Validation("email.too_long", $"Email must be at most {MaxLength} characters.");
        }

        return EmailPattern().IsMatch(normalized)
            ? new Email(normalized)
            : Error.Validation("email.invalid_format", "Email is not a valid email address.");
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)]
    private static partial Regex EmailPattern();
}
