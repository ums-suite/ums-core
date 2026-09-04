namespace UMS.Modules.Documents.Domain.BulkJobs;

/// <summary>
/// edge-cases.md's per-item checkpoint/resume decision: each item transitions independently, and
/// a crash mid-batch resumes by re-querying for not-yet-<see cref="Completed"/> items directly,
/// never by re-deriving progress or re-rendering an already-<see cref="Completed"/> item.
/// </summary>
public enum BulkGenerationJobItemStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,

    /// <summary>Exhausted its bounded retry budget (requirement-spec.md documents §2/§5/§8) - dead-lettered, never blocks the rest of the batch.</summary>
    DeadLettered = 3,
}
