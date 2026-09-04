using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Modules.Documents.Domain.Templates;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Generation;

/// <summary>
/// DOC-2/DOC-3/DOC-4/DOC-8/DOC-14/DOC-15's shared render-upload-verify-finalize mechanics - used by
/// both the synchronous single-document path (<see cref="GenerateDocumentService"/>) and the
/// bulk/async worker (<c>UMS.Workers</c>' <c>BulkGenerationRelayWorker</c>), so the two paths can
/// never drift on what "Ready" actually requires (requirement-spec.md documents §4's three-way
/// gate: render, upload, checksum verification).
/// </summary>
public sealed class GeneratedDocumentPipeline(
    IDocumentRenderer renderer,
    IObjectStorage objectStorage,
    IUnitOfWork unitOfWork,
    IOutboxEnqueuer outbox,
    IAuditRecorder auditRecorder,
    IClock clock,
    ILogger<GeneratedDocumentPipeline> logger)
{
    private const string RetryRequestedEventType = "DocumentGenerationRetryRequested";

    /// <summary>
    /// Runs render -&gt; upload -&gt; attach -&gt; checksum-verify -&gt; Ready against an
    /// already-claimed (<see cref="GeneratedDocumentStatus.Pending"/>) row. Every intermediate
    /// write is its own <see cref="IUnitOfWork.SaveChangesAsync"/> call (design-decisions.md's
    /// saga-style two-phase flow: attaching the artifact and marking Ready are deliberately two
    /// separate transactions, not one, so a crash between them never leaves a row falsely claiming
    /// <c>Ready</c>).
    /// </summary>
    public async Task<PipelineOutcome> RunAsync(GeneratedDocument document, DocumentTemplate template, string correlationId, CancellationToken cancellationToken)
    {
        byte[] renderedBytes;
        try
        {
            var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(document.RenderDataJson) ?? [];
            renderedBytes = renderer.Render(new DocumentRenderRequest(template, document.Language, fields, document.DigitalVerificationId, clock.UtcNow));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Document render failed for GeneratedDocument {DocumentId} [correlationId={CorrelationId}].", document.Id, correlationId);
            document.MarkFailed();
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return PipelineOutcome.Failed;
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(renderedBytes));

        // CA5351 flags MD5 as a "broken cryptographic algorithm" - not applicable here: this MD5
        // digest is never used for a security purpose, only to compare against S3's own ETag
        // (AWS's documented ETag = MD5-of-content behavior for a single-part PutObject) as a
        // cheap upload-integrity check. The publicly exposed/stored checksum is the SHA-256 above.
#pragma warning disable CA5351
        var md5 = Convert.ToHexStringLower(MD5.HashData(renderedBytes));
#pragma warning restore CA5351

        var objectKey = $"generated-documents/{document.DocumentType.ToString().ToLowerInvariant()}/{document.Id.Value}.pdf";

        string eTag;
        try
        {
            using var uploadStream = new MemoryStream(renderedBytes);
            eTag = await objectStorage.UploadAsync(objectKey, uploadStream, "application/pdf", cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // edge-cases.md's object-storage-outage-during-sync-receipt edge case: never mark this
            // Failed (which would require a brand-new claim to retry) - leave it Pending and
            // enqueue an async retry, so the triggering module's own already-committed transaction
            // is never blocked or reversed by an object-storage outage.
            logger.LogError(ex, "Document upload failed for GeneratedDocument {DocumentId} - queuing async retry [correlationId={CorrelationId}].", document.Id, correlationId);
            outbox.Enqueue(RetryRequestedEventType, JsonSerializer.Serialize(new DocumentGenerationRetryPayload(document.Id.Value)), clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return PipelineOutcome.StorageOutageRetryQueued;
        }

        var attach = document.AttachUploadedArtifact(objectKey, sha256, "application/pdf", renderedBytes.LongLength);
        if (attach.IsFailure)
        {
            logger.LogError("Unexpected AttachUploadedArtifact failure for GeneratedDocument {DocumentId}: {Error} [correlationId={CorrelationId}].", document.Id, attach.Error!.Code, correlationId);
            return PipelineOutcome.Failed;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Checksum verification (§4's third gate): S3's ETag for a single-part PutObject is the
        // MD5 of the uploaded bytes (AWS's own documented behavior) - comparing it to a locally
        // computed MD5 confirms the object actually stored matches what was rendered, without a
        // second round-trip download.
        var normalizedETag = eTag.Trim('"').ToLowerInvariant();
        if (normalizedETag != md5)
        {
            logger.LogError("Checksum mismatch for GeneratedDocument {DocumentId}: expected {ExpectedMd5}, storage ETag {ActualETag} [correlationId={CorrelationId}].", document.Id, md5, normalizedETag, correlationId);
            await CleanupOrphanedArtifactAsync(objectKey, cancellationToken).ConfigureAwait(false);
            document.MarkFailed();
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return PipelineOutcome.Failed;
        }

        var ready = document.MarkReady(clock.UtcNow);
        if (ready.IsFailure)
        {
            logger.LogError("Unexpected MarkReady failure for GeneratedDocument {DocumentId}: {Error} [correlationId={CorrelationId}].", document.Id, ready.Error!.Code, correlationId);
            return PipelineOutcome.Failed;
        }

        await FinalizeReadyAsync(document, correlationId, cancellationToken).ConfigureAwait(false);
        return PipelineOutcome.Ready;
    }

    /// <summary>DOC-14: commits the Ready transition and, for an official-record type, its Audit entry in the same database transaction (ADR-0012) - an audit-write failure must not leave a document silently Ready with no audit trail.</summary>
    private async Task FinalizeReadyAsync(GeneratedDocument document, string correlationId, CancellationToken cancellationToken)
    {
        if (!document.DocumentType.IsOfficialRecord())
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = new RecordAuditEntryRequest(
            ActorId: "system:document-generator",
            ActorType: AuditActorType.System,
            IpAddress: null,
            Application: "system",
            EntityType: "GeneratedDocument",
            EntityId: document.Id.Value.ToString(),
            Action: AuditActions.Create,
            BeforeValueJson: null,
            AfterValueJson: JsonSerializer.Serialize(new { documentType = document.DocumentType.ToString(), ownerId = document.OwnerId, digitalVerificationId = document.DigitalVerificationId }),
            CorrelationId: correlationId);

        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            logger.LogError("Audit recording failed for GeneratedDocument {DocumentId}: {Error} - rolling back the Ready transition [correlationId={CorrelationId}].", document.Id, auditResult.Error!.Code, correlationId);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException($"Audit recording failed for GeneratedDocument '{document.Id}': {auditResult.Error!.Message}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CleanupOrphanedArtifactAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await objectStorage.DeleteAsync(objectKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort - an orphaned object with no Ready row pointing at it is a storage-cost
            // leak, not a correctness problem (design-decisions.md's own accepted trade-off); the
            // sweep worker will find and retry deleting it later regardless.
            logger.LogWarning(ex, "Failed to delete orphaned object-storage artifact {ObjectKey} - will be retried by the sweep.", objectKey);
        }
    }
}
