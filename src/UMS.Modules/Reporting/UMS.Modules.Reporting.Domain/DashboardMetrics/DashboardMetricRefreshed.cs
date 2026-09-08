using UMS.Modules.Reporting.Domain.Common;

namespace UMS.Modules.Reporting.Domain.DashboardMetrics;

/// <summary>requirement-spec.md §3: fired when a <c>MetricRefreshJob</c> completes successfully - an internal cache-invalidation signal for the Admin dashboard read path (no consumer in this build; the event exists for that future wiring, mirroring how other modules ship events ahead of their first real consumer).</summary>
public sealed record DashboardMetricRefreshed(string MetricKey, DateTimeOffset OccurredAt) : IDomainEvent;
