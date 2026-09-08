using System.Text.Json;
using UMS.Modules.Reporting.Application.Abstractions;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>
/// The one application service every dashboard GET endpoint (RPT-4..9) calls - reporting
/// requirement-spec.md §2.1/§2.4: "every dashboard/report API response carries a <c>data_as_of</c>
/// (staleness) timestamp ... enforced as a required field on every Reporting response DTO."
/// <see cref="DashboardResponse"/> is that shared shape.
/// </summary>
public sealed class DashboardMetricReadService(IDashboardMetricRepository metrics)
{
    public async Task<DashboardResponse> GetAsync(string metricKey, CancellationToken cancellationToken = default)
    {
        var metric = await metrics.GetByKeyAsync(metricKey, cancellationToken).ConfigureAwait(false);

        // edge-cases.md "A dashboard is viewed for the first time before its first scheduled
        // refresh has ever run": an explicit "not yet computed" state, no fabricated placeholder.
        if (metric is null || !metric.HasEverBeenComputed)
        {
            return new DashboardResponse(DashboardMetricComputationStatus.NeverComputed, null, null);
        }

        var payload = JsonSerializer.Deserialize<JsonElement>(metric.PayloadJson!);
        return new DashboardResponse(DashboardMetricComputationStatus.Computed, metric.DataAsOf, payload);
    }
}
