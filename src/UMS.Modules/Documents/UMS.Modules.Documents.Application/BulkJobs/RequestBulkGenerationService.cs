using System.Text.Json;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.BulkJobs;

/// <summary>DOC-4/DOC-5/DOC-6: creates a <see cref="BulkGenerationJob"/> plus its per-item tracking rows, and enqueues the ADR-0014 outbox trigger the worker relay polls for (requirement-spec.md documents §2 Generation - Asynchronous/Bulk Path).</summary>
public sealed class RequestBulkGenerationService(
    IBulkGenerationJobRepository jobs,
    IBulkGenerationJobItemRepository jobItems,
    IDocumentTemplateRepository templates,
    IUnitOfWork unitOfWork,
    IOutboxEnqueuer outbox,
    IClock clock)
{
    public const string JobRequestedEventType = "BulkDocumentGenerationRequested";

    public async Task<Result<BulkGenerationJobDto>> RequestAsync(RequestBulkGenerationCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Items.Count == 0)
        {
            return Error.Validation("bulk_generation_job.items_required", "At least one item is required.");
        }

        // design-decisions.md's BulkGenerationJob template-version-pinning decision: resolved and
        // the job row inserted back-to-back, immediately before SaveChangesAsync commits both the
        // job and its item rows together - the closest this Application-layer flow gets to "the
        // same database transaction" without EF Core's own implicit transaction wrapping every
        // SaveChanges call already providing exactly that guarantee.
        var template = await templates.GetCurrentAsync(command.DocumentType, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return Error.NotFound("document.template_not_found", $"No published DocumentTemplate exists for type '{command.DocumentType}'.");
        }

        var job = BulkGenerationJob.Create(command.DocumentType, template.Id, template.Version, command.RequestedByUserId, command.Items.Count, clock.UtcNow);
        if (job.IsFailure)
        {
            return job.Error!;
        }

        jobs.Add(job.Value);

        var items = command.Items
            .Select(i => BulkGenerationJobItem.Create(job.Value.Id, i.OwnerId, i.SourceReferenceId, JsonSerializer.Serialize(i.Fields)))
            .ToList();
        jobItems.AddRange(items);

        outbox.Enqueue(JobRequestedEventType, JsonSerializer.Serialize(new BulkGenerationJobRequestedPayload(job.Value.Id.Value)), clock.UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return BulkGenerationJobDto.FromDomain(job.Value);
    }
}
