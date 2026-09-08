using UMS.Modules.Reporting.Domain.Common;

namespace UMS.Modules.Reporting.Domain.DashboardMetrics;

/// <summary>
/// reporting requirement-spec.md §3: "A named, cross-module derived value, recomputed on a
/// schedule; carries <c>data_as_of</c>." Keyed by <see cref="Id"/> - a stable per-dashboard-family
/// business key (e.g. <c>"academic-dashboard"</c>), one row per dashboard family (RPT-4..9) rather
/// than one row per individual tile figure: each dashboard's whole read-model payload is stored as
/// one JSON blob (<see cref="PayloadJson"/>) computed by that dashboard's own
/// <c>MetricRefreshJob</c>. This is a documented first-pass simplification - "three similar lines is
/// better than a premature abstraction" applies equally to schema granularity: a "base" reporting
/// flow does not need a separately-versioned row per tile when every tile within one dashboard is
/// always recomputed together, by the same job, on the same cadence.
///
/// <para>
/// <b>The module's two foundational invariants, enforced here, not just described in application
/// code (requirement-spec.md §4):</b> <see cref="RecordSuccessfulRefresh"/> is the only way
/// <see cref="PayloadJson"/>/<see cref="DataAsOf"/> ever change - always together, never
/// separately, so a payload and its own staleness timestamp can never disagree about what instant
/// they describe. <see cref="RecordFailedRefresh"/> deliberately touches NEITHER field - "a metric
/// refresh failure never silently substitutes a default/zero value" is therefore a property this
/// aggregate cannot violate by construction, not merely a rule the calling job is trusted to
/// follow.
/// </para>
/// </summary>
public sealed class DashboardMetric : AggregateRoot<string>
{
    private DashboardMetric()
    {
    }

    private DashboardMetric(string metricKey, string displayName, DateTimeOffset now)
    {
        Id = metricKey;
        DisplayName = displayName;
        CreatedAt = now;
    }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>The dashboard's own already-aggregated read-model, serialized - <see langword="null"/> until the first successful refresh ever completes (edge-cases.md "A dashboard is viewed for the first time before its first scheduled refresh has ever run").</summary>
    public string? PayloadJson { get; private set; }

    /// <summary>The staleness timestamp requirement-spec.md §2.4 mandates on every response - the wall-clock instant the successful run that produced <see cref="PayloadJson"/> began. <see langword="null"/> iff <see cref="PayloadJson"/> is <see langword="null"/> (never computed).</summary>
    public DateTimeOffset? DataAsOf { get; private set; }

    public DateTimeOffset? LastRefreshAttemptedAt { get; private set; }

    public bool LastRefreshSucceeded { get; private set; }

    public string? LastRefreshError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>edge-cases.md "A dashboard is viewed for the first time before its first scheduled refresh has ever run" - the explicit "not yet computed" state, distinct from a stale-but-previously-computed response.</summary>
    public bool HasEverBeenComputed => PayloadJson is not null;

    public static DashboardMetric Create(string metricKey, string displayName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(metricKey))
        {
            throw new ArgumentException("A DashboardMetric key is required.", nameof(metricKey));
        }

        return new DashboardMetric(metricKey, displayName, now);
    }

    /// <summary>design-decisions.md "Snapshot-Timestamp Pinning": <paramref name="asOf"/> is the same wall-clock instant the calling job captured at its own run start, before any source-module call - the value this response's own <c>data_as_of</c> stamps.</summary>
    public void RecordSuccessfulRefresh(string payloadJson, DateTimeOffset asOf)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadJson);

        PayloadJson = payloadJson;
        DataAsOf = asOf;
        LastRefreshAttemptedAt = asOf;
        LastRefreshSucceeded = true;
        LastRefreshError = null;

        Raise(new DashboardMetricRefreshed(Id, asOf));
    }

    /// <summary>requirement-spec.md §4: retain-and-alert - <see cref="PayloadJson"/>/<see cref="DataAsOf"/> are left exactly as they were.</summary>
    public void RecordFailedRefresh(string error, DateTimeOffset attemptedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        LastRefreshAttemptedAt = attemptedAt;
        LastRefreshSucceeded = false;
        LastRefreshError = error;

        Raise(new DashboardMetricRefreshFailed(Id, error, attemptedAt));
    }
}
