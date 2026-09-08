namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>requirement-spec.md §2.3/§4: a run is always async - enqueued as <see cref="Pending"/>, picked up by the worker (<see cref="Running"/>), and resolves to exactly one terminal state.</summary>
public enum RegulatoryReportRunStatus
{
    Pending,
    Running,
    Completed,
    Failed,
}
