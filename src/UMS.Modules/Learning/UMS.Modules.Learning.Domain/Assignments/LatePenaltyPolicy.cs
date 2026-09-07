using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>
/// design-decisions.md "Late-Penalty &amp; Grace-Period Model for <c>SubmissionWindow</c>": an
/// ordered list of <c>(maxLateness, deductionPercentage)</c> tiers, nested inside
/// <see cref="SubmissionWindow"/> and configurable per <see cref="Assignment"/>. A single-tier
/// policy IS a flat-percentage policy, which is exactly why that decision generalizes rather than
/// maintaining two parallel code paths for the simple and rich shapes.
///
/// <para>
/// Tiers must be strictly ascending by <see cref="LatePenaltyTier.MaxLateness"/> and
/// non-decreasing by <see cref="LatePenaltyTier.DeductionPercentage"/> - a schedule that penalized a
/// later submission LESS than an earlier one would be nonsense, so it is rejected at construction
/// rather than silently accepted and mis-evaluated later.
/// </para>
///
/// <para>
/// Lateness past the final tier's bound (but still before <c>hardCloseAt</c>) carries that final
/// tier's deduction - the schedule's last rung is its ceiling, not a cliff back to zero penalty.
/// </para>
/// </summary>
public sealed record LatePenaltyPolicy
{
    private readonly List<LatePenaltyTier> _tiers;

    private LatePenaltyPolicy(List<LatePenaltyTier> tiers)
    {
        _tiers = tiers;
    }

    /// <summary>The platform default design-decisions.md names: "a sensible platform default (a single generous tier) means an Instructor who wants the simple case never has to think about tiers at all".</summary>
    public static LatePenaltyPolicy NoDeduction { get; } = new([]);

    public IReadOnlyList<LatePenaltyTier> Tiers => _tiers.AsReadOnly();

    public static Result<LatePenaltyPolicy> Create(IReadOnlyList<LatePenaltyTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        for (var i = 1; i < tiers.Count; i++)
        {
            if (tiers[i].MaxLateness <= tiers[i - 1].MaxLateness)
            {
                return Error.Validation("late_penalty_policy.tiers_not_ascending", "Late-penalty tiers must be ordered by strictly ascending maxLateness.");
            }

            if (tiers[i].DeductionPercentage < tiers[i - 1].DeductionPercentage)
            {
                return Error.Validation("late_penalty_policy.deduction_decreases", "A later late-penalty tier may never deduct less than an earlier one.");
            }
        }

        return new LatePenaltyPolicy([.. tiers]);
    }

    /// <summary>The deduction (0-100) applicable to a submission <paramref name="lateness"/> past the effective deadline. Zero for a non-late submission or an empty policy.</summary>
    public decimal DeductionFor(TimeSpan lateness)
    {
        if (lateness <= TimeSpan.Zero || _tiers.Count == 0)
        {
            return 0m;
        }

        foreach (var tier in _tiers)
        {
            if (lateness <= tier.MaxLateness)
            {
                return tier.DeductionPercentage;
            }
        }

        return _tiers[^1].DeductionPercentage;
    }
}
