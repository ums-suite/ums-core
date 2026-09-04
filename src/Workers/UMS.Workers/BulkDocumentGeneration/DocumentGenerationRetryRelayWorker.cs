using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Workers.BulkDocumentGeneration;

/// <summary>
/// DOC-15: the worker side of the synchronous path's object-storage-outage fallback
/// (edge-cases.md: "falls back to an async retry and the calling module's transaction is
/// unaffected"). Polls Documents' own outbox for "DocumentGenerationRetryRequested" triggers
/// (enqueued by <see cref="GeneratedDocumentPipeline"/> when a synchronous or bulk-item upload
/// fails) and re-runs the same shared pipeline against the still-<c>Pending</c> claim.
/// </summary>
public sealed class DocumentGenerationRetryRelayWorker(IServiceScopeFactory scopeFactory, ILogger<DocumentGenerationRetryRelayWorker> logger) : BackgroundService
{
    private const string RetryRequestedEventType = "DocumentGenerationRetryRequested";
    private const int BatchSize = 20;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRetriesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Document generation retry relay: unexpected failure while processing pending retries.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingRetriesAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();

        var messages = await outbox.GetUnprocessedAsync(RetryRequestedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            await ProcessOneAsync(scope.ServiceProvider, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(IServiceProvider services, OutboxMessageDto message, CancellationToken cancellationToken)
    {
        var outbox = services.GetRequiredService<IOutboxReader>();
        var documents = services.GetRequiredService<IGeneratedDocumentRepository>();
        var templates = services.GetRequiredService<IDocumentTemplateRepository>();
        var pipeline = services.GetRequiredService<GeneratedDocumentPipeline>();
        var notifications = services.GetRequiredService<INotificationRequestPublisher>();

        try
        {
            var payload = JsonSerializer.Deserialize<DocumentGenerationRetryPayload>(message.PayloadJson)
                ?? throw new InvalidOperationException("Empty DocumentGenerationRetryRequested payload.");

            var document = await documents.GetByIdAsync(new GeneratedDocumentId(payload.GeneratedDocumentId), cancellationToken).ConfigureAwait(false);
            if (document is null || document.Status == GeneratedDocumentStatus.Ready)
            {
                // Already resolved by a previous, since-crashed attempt, or the row was since
                // revoked/superseded/deleted - ack and move on (ADR-0014: idempotent by
                // construction).
                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                return;
            }

            var template = await templates.GetByIdAsync(document.TemplateId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"DocumentTemplate '{document.TemplateId}' referenced by GeneratedDocument '{document.Id}' no longer exists.");

            var outcome = await pipeline.RunAsync(document, template, message.Id.ToString(), cancellationToken).ConfigureAwait(false);

            if (outcome == PipelineOutcome.Ready && document.RequestedByUserId is { } recipientId)
            {
                await notifications.PublishAsync(
                    new NotificationRequest(recipientId, "DocumentGenerated", $"Your {document.DocumentType} is ready.", message.Id.ToString()),
                    cancellationToken).ConfigureAwait(false);
            }

            // A repeat StorageOutageRetryQueued outcome enqueues its own fresh retry message
            // (GeneratedDocumentPipeline.RunAsync's own outbox.Enqueue call) - this message itself
            // is still marked processed either way, since the retry chain now continues via that
            // new message rather than this one being re-attempted.
            await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Document generation retry relay: failed to process outbox message {MessageId}.", message.Id);
            await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}
