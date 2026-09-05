using System.Globalization;
using System.Text.RegularExpressions;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Domain;

/// <summary>
/// A year-scoped academic session code (e.g. <c>"2025-2026"</c>) (ums-conventions.md, Domain
/// Modeling: "AcademicSessionCode ... "). Scaffolded here as part of Academic (release/
/// DEVELOPMENT_PLAN.md Flow #12; requirement-spec.md academic §2 "AcademicSession is a year-scoped
/// period") - the first module owning the <c>AcademicSession</c> concept; every later module
/// referencing a session period by code reuses this instead of a bare <c>string</c>.
/// </summary>
public sealed partial record AcademicSessionCode
{
    private AcademicSessionCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static Result<AcademicSessionCode> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Error.Validation("academic_session_code.required", "AcademicSessionCode is required.");
        }

        var trimmed = value.Trim();
        if (!SessionCodePattern().IsMatch(trimmed))
        {
            return Error.Validation("academic_session_code.invalid_format", "AcademicSessionCode must be in 'YYYY-YYYY' format (e.g. '2025-2026').");
        }

        var years = trimmed.Split('-');
        if (int.Parse(years[1], CultureInfo.InvariantCulture) != int.Parse(years[0], CultureInfo.InvariantCulture) + 1)
        {
            return Error.Validation("academic_session_code.invalid_range", "AcademicSessionCode's second year must be exactly one greater than its first year.");
        }

        return new AcademicSessionCode(trimmed);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^\d{4}-\d{4}$", RegexOptions.Compiled)]
    private static partial Regex SessionCodePattern();
}
