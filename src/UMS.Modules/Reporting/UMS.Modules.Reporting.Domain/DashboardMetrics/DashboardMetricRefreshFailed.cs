using UMS.Modules.Reporting.Domain.Common;

namespace UMS.Modules.Reporting.Domain.DashboardMetrics;

/// <summary>requirement-spec.md §3: fired when a <c>MetricRefreshJob</c> fails after its retry budget. §5 Observability: Reporting is not in the 100%-trace/paging tier - this is consumed as a structured log line at a distinguishable level (see <c>MetricRefreshJobBase</c>), never a real paging integration (none exists in this codebase to hook into).</summary>
public sealed record DashboardMetricRefreshFailed(string MetricKey, string Error, DateTimeOffset OccurredAt) : IDomainEvent;
