namespace UMS.Modules.Reporting.Application.Abstractions;

/// <summary>The server's own clock - never <c>DateTimeOffset.UtcNow</c> read inline. Mirrors every other module's own <c>IClock</c> exactly. Reporting's own <c>MetricRefreshJobBase</c>/<c>RegulatoryReportRunExecutionService</c> capture <see cref="UtcNow"/> exactly once per run as the run's own <c>as_of</c>/<c>data_as_of</c> instant (design-decisions.md "Snapshot-Timestamp Pinning" - this build's documented fallback, see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks).</summary>
public interface IClock
{
    public DateTimeOffset UtcNow { get; }
}
