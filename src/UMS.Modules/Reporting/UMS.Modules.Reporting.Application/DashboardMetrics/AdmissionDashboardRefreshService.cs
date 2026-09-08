using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Admission;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>
/// RPT-5: the one dashboard whose own cadence structurally changes based on campaign state
/// (requirement-spec.md §2.2 Admission row; edge-cases.md "An admission campaign starts or ends
/// mid-cycle"). <see cref="DetermineNextPollIntervalAsync"/> is what
/// <c>UMS.Workers.Reporting.AdmissionMetricRefreshWorker</c> calls on every tick to self-determine
/// its OWN next interval - never a manually-toggled ops setting.
///
/// <para>
/// A "nightly" interval here is modeled as a fixed 24-hour poll period from process start, not a
/// specific wall-clock hour - a documented first-pass simplification consistent with every other
/// dashboard's own "poll on an interval" cadence shape, rather than introducing a cron-like
/// scheduler solely for this one job.
/// </para>
/// </summary>
public sealed class AdmissionDashboardRefreshService(
    IAdmissionReportingQuery query,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<AdmissionDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "admission-dashboard";

    public static readonly TimeSpan NearRealTimeInterval = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan NightlyInterval = TimeSpan.FromHours(24);

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Admission Dashboard";

    /// <summary>Generous relative to even the near-real-time 5-15 minute cadence, so a legitimately slow run is never mistaken for crashed mid-campaign.</summary>
    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(8);

    /// <summary>
    /// edge-cases.md "cadence-determination query itself fails ... falls back to whatever cadence
    /// the job was already running at" - treated identically to any other source-unavailability
    /// edge case (retain prior behavior, log, never guess).
    /// </summary>
    public async Task<TimeSpan> DetermineNextPollIntervalAsync(TimeSpan currentInterval, CancellationToken cancellationToken)
    {
        try
        {
            var isActive = await query.IsAnyCampaignActiveAsync(cancellationToken).ConfigureAwait(false);
            return isActive ? NearRealTimeInterval : NightlyInterval;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Admission dashboard: failed to resolve campaign-state-driven cadence - retaining current interval {CurrentInterval}.", currentInterval);
            return currentInterval;
        }
    }

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await query.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(snapshot);
    }
}
