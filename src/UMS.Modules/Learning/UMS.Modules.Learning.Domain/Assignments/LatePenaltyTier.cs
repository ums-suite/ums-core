using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>
/// One rung of the tiered late-penalty schedule design-decisions.md's "Late-Penalty &amp;
/// Grace-Period Model for <c>SubmissionWindow</c>" mandates: everything up to and including
/// <see cref="MaxLateness"/> past the effective deadline costs
/// <see cref="DeductionPercentage"/> of the awarded score.
/// </summary>
public sealed record LatePenaltyTier
{
    private LatePenaltyTier(TimeSpan maxLateness, decimal deductionPercentage)
    {
        MaxLateness = maxLateness;
        DeductionPercentage = deductionPercentage;
    }

    /// <summary>Inclusive upper bound of this tier, measured from the effective deadline (<c>deadline + gracePeriod</c>).</summary>
    public TimeSpan MaxLateness { get; }

    /// <summary>0-100. A tier at 100 means "accepted, but zero credit" - distinct from <c>hardCloseAt</c>, past which nothing is accepted at all.</summary>
    public decimal DeductionPercentage { get; }

    public static Result<LatePenaltyTier> Create(TimeSpan maxLateness, decimal deductionPercentage)
    {
        if (maxLateness <= TimeSpan.Zero)
        {
            return Error.Validation("late_penalty_tier.non_positive_max_lateness", "A late-penalty tier's maxLateness must be greater than zero.");
        }

        return deductionPercentage is < 0m or > 100m
            ? Error.Validation("late_penalty_tier.deduction_out_of_range", "A late-penalty tier's deductionPercentage must be between 0 and 100.")
            : new LatePenaltyTier(maxLateness, deductionPercentage);
    }
}
