using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Research;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>
/// Flow #26: keeps the "research-dashboard" <see cref="Domain.DashboardMetrics.DashboardMetric"/> row
/// fresh - the same RPT-1 lease-then-refresh mechanism every other dashboard family already uses.
/// Unlike the six base-flow dashboards, this one has no dedicated Admin-facing
/// <c>GET /api/v1/reporting/dashboards/...</c> route of its own (Research was never named in
/// requirement-spec.md §2.2's dashboard table) - its sole consumer is
/// <c>RegulatoryReportRunExecutionService</c>, which resolves the "Research" regulatory category's
/// <c>SourceQueryReferences</c> (<c>["research-dashboard"]</c>) against this same
/// <see cref="Abstractions.IDashboardMetricRepository"/> row, exactly like every other category
/// already does. Nightly cadence (<c>UMS.Workers.Reporting.ResearchMetricRefreshWorker</c>) - grant/
/// publication/repository figures are not near-real-time-sensitive.
/// </summary>
public sealed class ResearchDashboardRefreshService(
    IResearchReportingQuery query,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<ResearchDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "research-dashboard";

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Research Dashboard";

    /// <summary>Well above a nightly job's own expected worst-case runtime.</summary>
    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(30);

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await query.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(snapshot);
    }
}
