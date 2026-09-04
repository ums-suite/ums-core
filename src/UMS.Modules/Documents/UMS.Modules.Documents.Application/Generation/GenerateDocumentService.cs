using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Modules.Documents.Domain.Templates;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Generation;

/// <summary>
/// DOC-3/DOC-8/DOC-15: the synchronous single-document generation path (requirement-spec.md
/// documents §2 Generation - Synchronous Path, p95 &lt; 3s per §5/§9). Owns the claim-row
/// concurrency mechanism (edge-cases.md's idempotent-generation race decision) - the actual
/// render/upload/verify mechanics are shared with the bulk/async path via
/// <see cref="GeneratedDocumentPipeline"/>.
/// </summary>
public sealed class GenerateDocumentService(
    IGeneratedDocumentRepository documents,
    IDocumentTemplateRepository templates,
    IUnitOfWork unitOfWork,
    IObjectStorage objectStorage,
    IVerificationIdGenerator verificationIdGenerator,
    GeneratedDocumentPipeline pipeline,
    INotificationRequestPublisher notifications,
    IClock clock,
    ILogger<GenerateDocumentService> logger)
{
    /// <summary>The synchronous claim-wait budget (edge-cases.md: "falling back to a synchronous-timeout-shaped error if the winner is still rendering past the sync path's own p95&lt;3s budget", §5).</summary>
    private static readonly TimeSpan ClaimWaitBudget = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan ClaimPollInterval = TimeSpan.FromMilliseconds(150);

    private static readonly TimeSpan DownloadUrlExpiry = TimeSpan.FromMinutes(15);

    public async Task<Result<GeneratedDocumentDto>> GenerateAsync(GenerateDocumentCommand command, CancellationToken cancellationToken = default)
    {
        var existing = await documents.GetByNaturalKeyAsync(command.OwnerId, command.DocumentType, command.SourceReferenceId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return await ResolveExistingAsync(existing, command.CorrelationId, cancellationToken).ConfigureAwait(false);
        }

        var template = await templates.GetCurrentAsync(command.DocumentType, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return Error.NotFound("document.template_not_found", $"No published DocumentTemplate exists for type '{command.DocumentType}'.");
        }

        var claim = GeneratedDocument.Claim(
            command.OwnerId,
            command.DocumentType,
            command.SourceReferenceId,
            template.Id,
            template.Version,
            verificationIdGenerator.NewId(),
            JsonSerializer.Serialize(command.Fields),
            command.Language ?? LanguageCodeExtensions.Fallback,
            clock.UtcNow,
            command.RequestedByUserId);

        try
        {
            await documents.AddAsync(claim, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (documents.IsNaturalKeyViolation(ex))
        {
            // edge-cases.md's idempotent-generation race: another request's claim won the natural
            // key between our existence check and our insert - fall in behind it exactly as if we
            // had found it on the first read.
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Claim race lost for ({OwnerId}, {DocumentType}, {SourceReferenceId}) - waiting on the winner's in-flight render [correlationId={CorrelationId}].", command.OwnerId, command.DocumentType, command.SourceReferenceId, command.CorrelationId);
            }

            var winner = await documents.GetByNaturalKeyAsync(command.OwnerId, command.DocumentType, command.SourceReferenceId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Natural-key unique violation reported but no row was found immediately after - this indicates a genuine infrastructure defect, not a documented race.");
            return await ResolveExistingAsync(winner, command.CorrelationId, cancellationToken).ConfigureAwait(false);
        }

        return await RunPipelineAndRespondAsync(claim, template, command.CorrelationId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<GeneratedDocumentDto>> ResolveExistingAsync(GeneratedDocument existing, string correlationId, CancellationToken cancellationToken)
    {
        if (existing.Status == GeneratedDocumentStatus.Failed)
        {
            var reopen = existing.Reopen();
            if (reopen.IsFailure)
            {
                return reopen.Error!;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var template = await templates.GetByIdAsync(existing.TemplateId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"DocumentTemplate '{existing.TemplateId}' referenced by GeneratedDocument '{existing.Id}' no longer exists.");
            return await RunPipelineAndRespondAsync(existing, template, correlationId, cancellationToken).ConfigureAwait(false);
        }

        if (existing.Status is GeneratedDocumentStatus.Pending or GeneratedDocumentStatus.Uploaded)
        {
            return await WaitForResolutionAsync(existing, correlationId, cancellationToken).ConfigureAwait(false);
        }

        // Ready, Revoked, or Superseded - the natural key's idempotent no-op result (§4): return
        // the existing row's true current state rather than attempting to regenerate it.
        return await ToDtoAsync(existing, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>edge-cases.md's claim-wait decision: poll briefly for the in-flight winner to resolve, falling back to a timeout-shaped error past the sync budget rather than waiting indefinitely.</summary>
    private async Task<Result<GeneratedDocumentDto>> WaitForResolutionAsync(GeneratedDocument existing, string correlationId, CancellationToken cancellationToken)
    {
        var deadline = clock.UtcNow + ClaimWaitBudget;
        var current = existing;

        while (clock.UtcNow < deadline)
        {
            if (current.Status is GeneratedDocumentStatus.Ready or GeneratedDocumentStatus.Failed or GeneratedDocumentStatus.Revoked or GeneratedDocumentStatus.Superseded)
            {
                break;
            }

            await Task.Delay(ClaimPollInterval, cancellationToken).ConfigureAwait(false);
            current = await documents.GetByIdAsync(current.Id, cancellationToken).ConfigureAwait(false) ?? current;
        }

        if (current.Status == GeneratedDocumentStatus.Ready)
        {
            return await ToDtoAsync(current, cancellationToken).ConfigureAwait(false);
        }

        if (current.Status is GeneratedDocumentStatus.Revoked or GeneratedDocumentStatus.Superseded)
        {
            return await ToDtoAsync(current, cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning("Synchronous claim-wait budget exceeded for GeneratedDocument {DocumentId} (still {Status}) [correlationId={CorrelationId}].", current.Id, current.Status, correlationId);
        return Error.Conflict("document.generation_in_progress", "This document is still being generated by a concurrent request - retry shortly.");
    }

    private async Task<Result<GeneratedDocumentDto>> RunPipelineAndRespondAsync(GeneratedDocument document, DocumentTemplate template, string correlationId, CancellationToken cancellationToken)
    {
        var outcome = await pipeline.RunAsync(document, template, correlationId, cancellationToken).ConfigureAwait(false);

        if (outcome == PipelineOutcome.Failed)
        {
            return Error.Failure("document.generation_failed", "Document generation failed - see server logs for the underlying cause.");
        }

        // DOC-13: best-effort - a Notifications outage must never fail a generation that itself
        // succeeded, mirroring ADR-0009's "never a direct call the primary flow depends on."
        if (outcome == PipelineOutcome.Ready)
        {
            await PublishNotificationSafelyAsync(document, "DocumentGenerated", correlationId, cancellationToken).ConfigureAwait(false);
        }

        return await ToDtoAsync(document, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishNotificationSafelyAsync(GeneratedDocument document, string eventType, string correlationId, CancellationToken cancellationToken)
    {
        if (document.RequestedByUserId is not { } recipientId)
        {
            return;
        }

        try
        {
            await notifications.PublishAsync(
                new NotificationRequest(recipientId, eventType, $"Your {document.DocumentType} is ready.", correlationId),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "NotificationRequest publish failed for GeneratedDocument {DocumentId} - the document itself is unaffected [correlationId={CorrelationId}].", document.Id, correlationId);
        }
    }

    private async Task<GeneratedDocumentDto> ToDtoAsync(GeneratedDocument document, CancellationToken cancellationToken)
    {
        string? downloadUrl = null;
        if (document.Status == GeneratedDocumentStatus.Ready && document.StorageKey is not null)
        {
            downloadUrl = await objectStorage.GetDownloadUrlAsync(document.StorageKey, DownloadUrlExpiry, cancellationToken).ConfigureAwait(false);
        }

        return GeneratedDocumentDto.FromDomain(document, downloadUrl);
    }
}
