namespace UMS.Modules.Documents.Domain.BulkJobs;

/// <summary>ADR-0014's shared outbox/worker job shape applied to Documents (requirement-spec.md documents §3's module-local term).</summary>
public enum BulkGenerationJobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,

    /// <summary>Every item was processed, but at least one was dead-lettered (edge-cases.md's very-large-bulk-job note: "one bad record never fails the whole batch") - the job itself still reaches a terminal, non-retryable state.</summary>
    CompletedWithErrors = 3,
}
