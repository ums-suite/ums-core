namespace UMS.Modules.Library.Application.Common;

/// <summary>
/// requirement-spec.md §9 decisions 2/3 and Open Questions - every numeric policy the BRD leaves
/// unspecified, adopted as a configurable default rather than a hardcoded literal. Bound from
/// configuration (<c>Library:*</c>) by <c>DependencyInjection.AddLibraryModule</c>.
///
/// <para>
/// <b>Known, documented gap</b> (carried into the PR description, not resolved here):
/// <see cref="FineDailyRateBdt"/> and <see cref="DefaultReplacementCostBdt"/> stand in for
/// requirement-spec.md §9's own Open Question - "the exact fine rate schedule (per-day amount,
/// per-category variation, any grace period) is not specified in the BRD; deferred to a
/// librarian-configurable rate table." This build implements the configurable MECHANISM (a single
/// flat rate read from configuration) but not a concrete per-category rate catalog - that remains a
/// future data-modeling pass, exactly as the spec anticipates.
/// </para>
/// </summary>
public sealed class LibraryOptions
{
    public int MaxConcurrentLoansStudent { get; init; } = 3;

    public int MaxConcurrentLoansFaculty { get; init; } = 10;

    public int LoanPeriodDaysStudent { get; init; } = 14;

    public int LoanPeriodDaysFaculty { get; init; } = 30;

    public int MaxRenewalCount { get; init; } = 2;

    public int ReservationClaimWindowHours { get; init; } = 48;

    /// <summary>requirement-spec.md §9 Open Question - a flat placeholder rate, not a per-category catalog (see class remarks).</summary>
    public decimal FineDailyRateBdt { get; init; } = 10m;

    /// <summary>requirement-spec.md §8 "a replacement-cost Fine is generated" - the BRD names no concrete figure; a flat placeholder (see class remarks).</summary>
    public decimal DefaultReplacementCostBdt { get; init; } = 500m;
}
