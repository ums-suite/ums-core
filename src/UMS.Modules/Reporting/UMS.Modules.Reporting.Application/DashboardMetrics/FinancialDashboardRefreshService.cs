using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Finance;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>RPT-6: nightly, aligned to Finance's own daily reconciliation job (requirement-spec.md §2.2 Financial row) - both simply run nightly in this pass; this build does not literally chain the two jobs' schedules together (a documented, low-risk simplification since both share the same "once a day" cadence regardless).</summary>
public sealed class FinancialDashboardRefreshService(
    IFinanceReportingQuery query,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<FinancialDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "financial-dashboard";

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Financial Dashboard";

    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(30);

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await query.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(snapshot);
    }
}
