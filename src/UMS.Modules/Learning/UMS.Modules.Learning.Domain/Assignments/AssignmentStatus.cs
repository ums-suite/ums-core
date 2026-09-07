namespace UMS.Modules.Learning.Domain.Assignments;

/// <summary>requirement-spec.md learning §2 Assignment Publication: `Draft -> Published -> Closed`, plus an explicit `Cancelled` terminal state for the course-offering-cancellation case (§8).</summary>
public enum AssignmentStatus
{
    Draft,
    Published,
    Closed,
    Cancelled,
}
