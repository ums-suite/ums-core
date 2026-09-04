using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Application.BulkJobs;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Workers.BulkDocumentGeneration;

/// <summary>
/// DOC-4/DOC-6/DOC-7: the worker side of Documents' bulk/async generation path (ADR-0014's shared
/// outbox/worker pattern) - polls Documents' own outbox for "BulkDocumentGenerationRequested"
/// triggers, then processes each job's items in chunks, resuming from exactly where a prior crash
/// left off (edge-cases.md's resumability decision: re-queries <c>BulkGenerationJobItem</c> for
/// not-yet-resolved rows, never re-derives progress and never re-renders an item whose
/// <c>GeneratedDocument</c> already reached <c>Ready</c>). Mirrors the shape of Audit's own
/// <c>AuditExportRelayWorker</c>.
/// </summary>
public sealed class BulkGenerationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<BulkGenerationRelayWorker> logger) : BackgroundService
{
    private const int OutboxBatchSize = 5;
    private const int ItemChunkSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingJobsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Bulk generation relay: unexpected failure while processing pending jobs.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static async Task ProcessOneItemAsync(
        BulkGenerationJobItem item,
        BulkGenerationJob job,
        DocumentTemplate template,
        IGeneratedDocumentRepository documents,
        IUnitOfWork unitOfWork,
        GeneratedDocumentPipeline pipeline,
        IVerificationIdGenerator verificationIdGenerator,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var existing = await documents.GetByNaturalKeyAsync(item.OwnerId, job.DocumentType, item.SourceReferenceId, cancellationToken).ConfigureAwait(false);

        // Resumability (edge-cases.md): an item already Ready from a prior, since-crashed attempt
        // is never re-rendered - just recorded as completed against the artifact that already
        // exists.
        if (existing is { Status: GeneratedDocumentStatus.Ready })
        {
            item.MarkCompleted(existing.Id);
            job.RecordItemOutcome(deadLettered: false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        GeneratedDocument claim;
        if (existing is { Status: GeneratedDocumentStatus.Failed } failedExisting)
        {
            failedExisting.Reopen();
            claim = failedExisting;
        }
        else if (existing is null)
        {
            var newClaim = GeneratedDocument.Claim(
                item.OwnerId,
                job.DocumentType,
                item.SourceReferenceId,
                template.Id,
                template.Version,
                verificationIdGenerator.NewId(),
                item.RenderDataJson,
                LanguageCodeExtensions.Fallback,
                DateTimeOffset.UtcNow,
                bulkGenerationJobId: job.Id.Value);

            try
            {
                await documents.AddAsync(newClaim, cancellationToken).ConfigureAwait(false);
                claim = newClaim;
            }
            catch (Exception ex) when (documents.IsNaturalKeyViolation(ex))
            {
                // A cross-path race with a synchronous request for the identical natural key
                // (rare - both paths independently enforce the same idempotency invariant, DOC-3
                // and DOC-4/DOC-6 just happened to collide) - fall in behind whatever already
                // exists rather than deadlocking this item on a constraint it can never win.
                var winner = await documents.GetByNaturalKeyAsync(item.OwnerId, job.DocumentType, item.SourceReferenceId, cancellationToken).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Natural-key unique violation reported but no row was found immediately after.");

                if (winner.Status == GeneratedDocumentStatus.Ready)
                {
                    item.MarkCompleted(winner.Id);
                    job.RecordItemOutcome(deadLettered: false);
                    await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    return;
                }

                var deadLetteredOnRace = item.RecordFailure("A concurrent synchronous request claimed this document's natural key before the bulk item could - retry not attempted (cross-path race).");
                if (deadLetteredOnRace)
                {
                    job.RecordItemOutcome(deadLettered: true);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        else
        {
            // Pending or Uploaded - still in flight from a previous attempt (possibly one still
            // being retried by DocumentGenerationRetryRelayWorker); re-run the pipeline against
            // the same claim rather than starting a new one.
            claim = existing;
        }

        item.MarkProcessing();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var outcome = await pipeline.RunAsync(claim, template, correlationId, cancellationToken).ConfigureAwait(false);

        switch (outcome)
        {
            case PipelineOutcome.Ready:
                item.MarkCompleted(claim.Id);
                job.RecordItemOutcome(deadLettered: false);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break;

            case PipelineOutcome.Failed:
                var deadLettered = item.RecordFailure("Render or checksum verification failed - see server logs for the underlying GeneratedDocument.");
                if (deadLettered)
                {
                    job.RecordItemOutcome(deadLettered: true);
                }

                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                break;

            case PipelineOutcome.StorageOutageRetryQueued:
                // Left Pending/Processing on purpose - GetUnresolvedBatchAsync's filter already
                // includes non-terminal statuses, so the next chunk pass (or
                // DocumentGenerationRetryRelayWorker resolving the underlying claim first) picks
                // this item back up without any special-casing here.
                break;
        }
    }

    private async Task ProcessPendingJobsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();

        var messages = await outbox.GetUnprocessedAsync(RequestBulkGenerationService.JobRequestedEventType, OutboxBatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            await ProcessOneJobAsync(scope.ServiceProvider, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneJobAsync(IServiceProvider services, OutboxMessageDto message, CancellationToken cancellationToken)
    {
        var outbox = services.GetRequiredService<IOutboxReader>();
        var jobs = services.GetRequiredService<IBulkGenerationJobRepository>();
        var jobItems = services.GetRequiredService<IBulkGenerationJobItemRepository>();
        var templates = services.GetRequiredService<IDocumentTemplateRepository>();
        var documents = services.GetRequiredService<IGeneratedDocumentRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var pipeline = services.GetRequiredService<GeneratedDocumentPipeline>();
        var notifications = services.GetRequiredService<INotificationRequestPublisher>();
        var clock = services.GetRequiredService<IClock>();

        try
        {
            var payload = JsonSerializer.Deserialize<BulkGenerationJobRequestedPayload>(message.PayloadJson)
                ?? throw new InvalidOperationException("Empty BulkDocumentGenerationRequested payload.");

            var job = await jobs.GetByIdAsync(new BulkGenerationJobId(payload.JobId), cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"BulkGenerationJob '{payload.JobId}' referenced by outbox message '{message.Id}' no longer exists.");

            if (job.Status is BulkGenerationJobStatus.Completed or BulkGenerationJobStatus.CompletedWithErrors)
            {
                // Already fully processed by a previous, since-crashed attempt that got as far as
                // completing the job but not yet marking this outbox message processed
                // (ADR-0014: "idempotent by construction, safe to retry") - just ack the message.
                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                return;
            }

            job.MarkProcessing();
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // design-decisions.md's template-version-pinning decision: the job's own pinned
            // TemplateId/TemplateVersion is resolved once here and reused for every item in the
            // batch - a template published mid-batch never changes output partway through a run.
            var template = await templates.GetByIdAsync(job.TemplateId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"DocumentTemplate '{job.TemplateId}' pinned by BulkGenerationJob '{job.Id}' no longer exists.");

            var verificationIdGenerator = services.GetRequiredService<IVerificationIdGenerator>();

            IReadOnlyList<BulkGenerationJobItem> batch;
            while ((batch = await jobItems.GetUnresolvedBatchAsync(job.Id, ItemChunkSize, cancellationToken).ConfigureAwait(false)).Count > 0)
            {
                foreach (var item in batch)
                {
                    await ProcessOneItemAsync(item, job, template, documents, unitOfWork, pipeline, verificationIdGenerator, message.Id.ToString(), cancellationToken).ConfigureAwait(false);
                }
            }

            var complete = job.Complete(clock.UtcNow);
            if (complete.IsFailure)
            {
                throw new InvalidOperationException($"BulkGenerationJob '{job.Id}' could not be completed: {complete.Error!.Message}");
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await notifications.PublishAsync(
                new NotificationRequest(job.RequestedByUserId, "BulkDocumentGenerationCompleted", $"Your bulk {job.DocumentType} generation job finished ({job.CompletedCount - job.DeadLetteredCount}/{job.TotalItems} succeeded).", message.Id.ToString()),
                cancellationToken).ConfigureAwait(false);

            await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Bulk generation relay: failed to process outbox message {MessageId}.", message.Id);
            await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}
