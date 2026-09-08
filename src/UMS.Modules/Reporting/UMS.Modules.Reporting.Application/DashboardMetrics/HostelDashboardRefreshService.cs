using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Hostel;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>RPT-8: hourly (requirement-spec.md §2.2 Hostel row) - occupancy is referenced more frequently than academic/financial aggregates, though still explicitly not a live "is this bed free" check (that stays with Hostel's own endpoints - requirement-spec.md §1).</summary>
public sealed class HostelDashboardRefreshService(
    IHostelReportingQuery query,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<HostelDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "hostel-dashboard";

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Hostel Dashboard";

    /// <summary>Well below the hourly cadence, so a legitimately slow run never blocks more than a fraction of the next tick's own interval.</summary>
    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(15);

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await query.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(snapshot);
    }
}
