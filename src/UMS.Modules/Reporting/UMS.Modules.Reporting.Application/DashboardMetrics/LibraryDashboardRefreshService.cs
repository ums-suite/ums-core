using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Library;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>RPT-9: nightly (requirement-spec.md §2.2 Library row).</summary>
public sealed class LibraryDashboardRefreshService(
    ILibraryReportingQuery query,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<LibraryDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "library-dashboard";

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Library Dashboard";

    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(30);

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var snapshot = await query.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Serialize(snapshot);
    }
}
