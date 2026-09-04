using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Domain.BulkJobs;

/// <summary>
/// design-decisions.md's per-item checkpoint/resume decision: one row per batch item, independently
/// chunked/retryable/dead-letterable - the resume mechanism queries this table directly for
/// not-yet-<see cref="BulkGenerationJobItemStatus.Completed"/> items, with the
/// <c>GeneratedDocument</c>'s own natural-key uniqueness constraint retained as defense-in-depth,
/// never the primary resume mechanism (§8's ~250,000-item-scale note).
/// </summary>
public sealed class BulkGenerationJobItem
{
    private const int MaxAttempts = 3;

    private BulkGenerationJobItem()
    {
    }

    public BulkGenerationJobItemId Id { get; private init; }

    public BulkGenerationJobId JobId { get; private init; }

    public Guid OwnerId { get; private init; }

    public Guid SourceReferenceId { get; private init; }

    /// <summary>The calling module's per-item render data (field name -&gt; value), serialized - Documents never authors document content itself (requirement-spec.md documents §1), it only places these caller-supplied values into the pinned template.</summary>
    public string RenderDataJson { get; private init; } = "{}";

    public BulkGenerationJobItemStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public GeneratedDocumentId? GeneratedDocumentId { get; private set; }

    public string? ErrorMessage { get; private set; }

    public static BulkGenerationJobItem Create(BulkGenerationJobId jobId, Guid ownerId, Guid sourceReferenceId, string renderDataJson) => new()
    {
        Id = BulkGenerationJobItemId.New(),
        JobId = jobId,
        OwnerId = ownerId,
        SourceReferenceId = sourceReferenceId,
        RenderDataJson = renderDataJson,
        Status = BulkGenerationJobItemStatus.Pending,
    };

    public Result MarkProcessing()
    {
        if (Status == BulkGenerationJobItemStatus.Completed)
        {
            return Result.Failure(Error.Conflict("bulk_generation_job_item.already_completed", "Cannot re-process an already-completed item."));
        }

        AttemptCount++;
        Status = BulkGenerationJobItemStatus.Processing;
        return Result.Success();
    }

    public Result MarkCompleted(GeneratedDocumentId generatedDocumentId)
    {
        Status = BulkGenerationJobItemStatus.Completed;
        GeneratedDocumentId = generatedDocumentId;
        ErrorMessage = null;
        return Result.Success();
    }

    /// <summary>
    /// requirement-spec.md documents §2/§5/§8: "chunked, individually retryable, and dead-lettered
    /// per-item - one bad record never fails the whole batch." Returns whether this attempt's
    /// failure exhausted the retry budget (dead-lettered) or should be retried on the next sweep.
    /// </summary>
    public bool RecordFailure(string errorMessage)
    {
        ErrorMessage = errorMessage;

        if (AttemptCount >= MaxAttempts)
        {
            Status = BulkGenerationJobItemStatus.DeadLettered;
            return true;
        }

        Status = BulkGenerationJobItemStatus.Pending;
        return false;
    }
}
