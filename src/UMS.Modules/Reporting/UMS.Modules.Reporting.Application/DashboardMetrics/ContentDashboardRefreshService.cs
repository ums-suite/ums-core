using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Content;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>
/// Flow #26: the seventh admin dashboard family, keeping the "content-dashboard"
/// <see cref="Domain.DashboardMetrics.DashboardMetric"/> row fresh via the identical RPT-1
/// lease-then-refresh mechanism the original six dashboards already use - see
/// <c>UMS.Shared.Content.IContentReportingQuery</c>'s own remarks for why this is a deliberate,
/// documented scope extension rather than an item Reporting's own requirement-spec.md §2.2 named.
/// Served by <c>GET /api/v1/reporting/dashboards/content</c>. Nightly cadence
/// (<c>UMS.Workers.Reporting.ContentMetricRefreshWorker</c>) - notice/banner/event/download figures
/// are not near-real-time-sensitive the way Admission's own dashboard is.
/// </summary>
public sealed class ContentDashboardRefreshService(
    IContentReportingQuery query,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<ContentDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "content-dashboard";

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Content Dashboard";

    /// <summary>Well above a nightly job's own expected worst-case runtime.</summary>
    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(30);

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await query.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(snapshot);
    }
}
