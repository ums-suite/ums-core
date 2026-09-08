namespace UMS.Modules.Hostel.Application.Common;

/// <summary>
/// requirement-spec.md §9 decision 3: "a configurable default (proposed: 7 days)" for the
/// fee-payment grace period. design-decisions.md "Complaint Deduplication Mechanism": the 60-second
/// server-side dedupe window fallback. Bound from configuration (<c>Hostel:GracePeriodDays</c>,
/// <c>Hostel:ComplaintDedupeWindowSeconds</c>) by <c>DependencyInjection.AddHostelModule</c>, with
/// these values as the documented defaults when unset.
/// </summary>
public sealed class HostelOptions
{
    public int GracePeriodDays { get; init; } = 7;

    public int ComplaintDedupeWindowSeconds { get; init; } = 60;

    /// <summary>HOS-15's grace window for a Complaint filed shortly after check-out (requirement-spec.md §8 edge case).</summary>
    public int ComplaintPostCheckOutGraceDays { get; init; } = 7;
}
