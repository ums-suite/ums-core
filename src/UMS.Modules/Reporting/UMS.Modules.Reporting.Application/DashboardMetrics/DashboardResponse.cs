using System.Text.Json;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

public enum DashboardMetricComputationStatus
{
    NeverComputed,
    Computed,
}

/// <summary>requirement-spec.md §2.4: <see cref="DataAsOf"/> is mandatory whenever <see cref="Status"/> is <see cref="DashboardMetricComputationStatus.Computed"/> - a consumer (Admin UI) can and should visibly flag a <see cref="DataAsOf"/> older than the metric's own expected cadence, rather than Reporting attempting to hide staleness (§2.1).</summary>
public sealed record DashboardResponse(DashboardMetricComputationStatus Status, DateTimeOffset? DataAsOf, JsonElement? Payload);
