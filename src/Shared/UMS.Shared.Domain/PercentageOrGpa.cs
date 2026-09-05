using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Domain;

/// <summary>The scale a <see cref="PercentageOrGpa"/> value is expressed on.</summary>
public enum PercentageOrGpaScale
{
    /// <summary>0-100.</summary>
    Percentage,

    /// <summary>0-<see cref="PercentageOrGpa.MaxGpaScale"/> (4.0 by default - configurable per institution, never hardcoded past this value object's own bound).</summary>
    Gpa,
}

/// <summary>
/// A bounded, scale-aware academic score (ums-conventions.md, Domain Modeling: "PercentageOrGpa
/// (bounded and scale-aware - never a raw <c>decimal</c> a caller could set to <c>6.0</c>)").
/// Scaffolded here as part of Academic (release/DEVELOPMENT_PLAN.md Flow #12; requirement-spec.md
/// academic §2 Grade Entry - the aggregate <c>Grade</c> score) since Grade calculation is the first
/// concept in this codebase needing it - every later module (Admission's own result percentage,
/// Reporting's GPA dashboards) reuses this instead of re-declaring its own bounded-score type.
/// </summary>
public sealed record PercentageOrGpa
{
    public const decimal MaxGpaScale = 4.0m;

    private PercentageOrGpa(decimal value, PercentageOrGpaScale scale)
    {
        Value = value;
        Scale = scale;
    }

    public decimal Value { get; }

    public PercentageOrGpaScale Scale { get; }

    public static Result<PercentageOrGpa> CreatePercentage(decimal value) =>
        value < 0 || value > 100
            ? Error.Validation("percentage_or_gpa.out_of_range", "A percentage must be between 0 and 100.")
            : new PercentageOrGpa(value, PercentageOrGpaScale.Percentage);

    public static Result<PercentageOrGpa> CreateGpa(decimal value) =>
        value < 0 || value > MaxGpaScale
            ? Error.Validation("percentage_or_gpa.out_of_range", $"A GPA must be between 0 and {MaxGpaScale}.")
            : new PercentageOrGpa(value, PercentageOrGpaScale.Gpa);

    public override string ToString() => Scale == PercentageOrGpaScale.Percentage ? $"{Value:0.##}%" : $"{Value:0.00} GPA";
}
