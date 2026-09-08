using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Academic;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>RPT-4: nightly steady-state cadence (requirement-spec.md §2.2 Academic row) - the poll interval itself is owned by <c>UMS.Workers.Reporting.AcademicMetricRefreshWorker</c>, not this service.</summary>
public sealed class AcademicDashboardRefreshService(
    IAcademicReportingQuery query,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<AcademicDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "academic-dashboard";

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Academic Dashboard";

    /// <summary>Well above a nightly job's own expected worst-case runtime.</summary>
    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(30);

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await query.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(snapshot);
    }
}
