using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Domain.BulkJobs;

/// <summary>
/// DOC-4: tracks one async batch generation request (requirement-spec.md documents §3's
/// module-local term - "not itself a glossary aggregate, just the ADR-0014 outbox/worker job shape
/// applied to this module"). Deliberately does <b>not</b> hold its <see cref="BulkGenerationJobItem"/>
/// children as an in-memory navigation collection - at ~250,000-item scale (§8) that would force
/// loading the entire batch into memory for any read of the job itself. Items are their own
/// EF-mapped entity with their own repository, keyed by <see cref="Id"/> as a foreign key; this
/// aggregate only tracks the running counters the worker updates as each item resolves.
/// </summary>
public sealed class BulkGenerationJob
{
    private BulkGenerationJob()
    {
    }

    public BulkGenerationJobId Id { get; private init; }

    public DocumentType DocumentType { get; private init; }

    public DocumentTemplateId TemplateId { get; private init; }

    /// <summary>design-decisions.md's template-version-pinning decision: resolved and recorded in the same database transaction that creates this row - a concurrent publish can never leave this job's pinned version ambiguous.</summary>
    public int TemplateVersion { get; private init; }

    public Guid RequestedByUserId { get; private init; }

    public BulkGenerationJobStatus Status { get; private set; }

    public int TotalItems { get; private init; }

    public int CompletedCount { get; private set; }

    public int DeadLetteredCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static Result<BulkGenerationJob> Create(
        DocumentType documentType,
        DocumentTemplateId templateId,
        int templateVersion,
        Guid requestedByUserId,
        int totalItems,
        DateTimeOffset now)
    {
        if (totalItems < 1)
        {
            return Error.Validation("bulk_generation_job.items_required", "A bulk generation job requires at least one item - a single-item request should use the synchronous endpoint instead (requirement-spec.md documents §4).");
        }

        return new BulkGenerationJob
        {
            Id = BulkGenerationJobId.New(),
            DocumentType = documentType,
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            RequestedByUserId = requestedByUserId,
            Status = BulkGenerationJobStatus.Pending,
            TotalItems = totalItems,
            CreatedAt = now,
        };
    }

    public Result MarkProcessing()
    {
        if (Status is BulkGenerationJobStatus.Completed or BulkGenerationJobStatus.CompletedWithErrors)
        {
            return Result.Failure(Error.Conflict("bulk_generation_job.already_terminal", $"Cannot resume processing a job already in terminal status '{Status}'."));
        }

        Status = BulkGenerationJobStatus.Processing;
        return Result.Success();
    }

    /// <summary>Advances the running counters as the worker resolves items - called once per item, so a resumed job's counters always reflect its item-tracking table exactly (edge-cases.md's resumability decision).</summary>
    public void RecordItemOutcome(bool deadLettered)
    {
        CompletedCount++;
        if (deadLettered)
        {
            DeadLetteredCount++;
        }
    }

    /// <summary>Called once every item has resolved (Completed or DeadLettered) - never before, so <see cref="CompletedAt"/> always reflects genuine completion, not merely "the worker stopped polling."</summary>
    public Result Complete(DateTimeOffset now)
    {
        if (CompletedCount < TotalItems)
        {
            return Result.Failure(Error.Conflict("bulk_generation_job.incomplete", $"Cannot complete a job with {CompletedCount}/{TotalItems} items resolved."));
        }

        Status = DeadLetteredCount > 0 ? BulkGenerationJobStatus.CompletedWithErrors : BulkGenerationJobStatus.Completed;
        CompletedAt = now;
        return Result.Success();
    }
}
