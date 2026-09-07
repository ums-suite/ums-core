namespace UMS.Modules.Learning.Domain.PlagiarismChecks;

/// <summary>
/// design-decisions.md "PlagiarismCheck Execution Timing &amp; Resilience". <see cref="Failed"/> is
/// a real, terminal, Instructor-visible status - never silently reinterpreted as "clean", and never
/// left in an ambiguous "still checking" state indefinitely: "an Instructor needs to be able to
/// tell 'checked, low similarity' apart from 'never successfully checked'".
/// <see cref="Cancelled"/> is what a check whose Submission was superseded before it completed
/// becomes, so provider quota is not spent on content a resubmission already made moot.
/// </summary>
public enum PlagiarismCheckStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}
