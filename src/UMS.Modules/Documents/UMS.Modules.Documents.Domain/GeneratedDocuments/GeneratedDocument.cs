using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Domain.GeneratedDocuments;

/// <summary>
/// DOC-2: the metadata-only aggregate root for one system-rendered document (requirement-spec.md
/// documents §3; ADR-0010: "artifact bytes live in object storage, never in this row"). No PDF (or
/// any artifact) byte array is ever a property here (§4's first invariant) - only the object-storage
/// key, checksum, and descriptive metadata.
///
/// <para>
/// The <c>(OwnerId, DocumentType, SourceReferenceId)</c> tuple is this aggregate's natural key
/// (§4: "makes generation idempotent") - enforced as a real database unique constraint
/// (<c>Persistence.Configurations.GeneratedDocumentConfiguration</c>), not merely an
/// application-level check, so the claim-row race (edge-cases.md) resolves correctly under real
/// concurrency rather than only under sequential-request testing.
/// </para>
/// </summary>
public sealed class GeneratedDocument
{
    private GeneratedDocument()
    {
    }

    public GeneratedDocumentId Id { get; private init; }

    /// <summary>
    /// An opaque cross-module reference (a Student/Applicant/User id from whichever module
    /// triggered generation) - Documents depends on no other module (requirement-spec.md documents
    /// §7), so this is deliberately a bare <see cref="Guid"/> rather than another module's own
    /// strongly-typed id, exactly as Audit's own <c>AuditLogEntry.EntityId</c> stores every other
    /// module's ids opaquely.
    /// </summary>
    public Guid OwnerId { get; private init; }

    public DocumentType DocumentType { get; private init; }

    /// <summary>
    /// The natural key's third component - an opaque reference to the specific source event/record
    /// that justified this document (a <c>PaymentId</c> for a receipt, a <c>ResultPublicationId</c>
    /// for a transcript, a <c>StudentRequestId</c> for an ID-card reissue). Together with
    /// <see cref="OwnerId"/> and <see cref="DocumentType"/>, makes a retried generation request
    /// idempotent (§4).
    /// </summary>
    public Guid SourceReferenceId { get; private init; }

    public DocumentTemplateId TemplateId { get; private init; }

    /// <summary>Captured at creation and never re-resolved - a later template publish must never change what an already-in-flight or already-rendered document says it was rendered from (§2/§4).</summary>
    public int TemplateVersion { get; private init; }

    public GeneratedDocumentStatus Status { get; private set; }

    public string? StorageKey { get; private set; }

    /// <summary>SHA-256 hex digest of the rendered artifact, set once the upload's checksum verification succeeds.</summary>
    public string? Checksum { get; private set; }

    public string? MimeType { get; private set; }

    public long? SizeBytes { get; private set; }

    /// <summary>
    /// DOC-8: unique and unguessable, assigned at creation (before rendering, since the QR code
    /// embedded in the rendered PDF encodes the verify URL built from this id) - never reused, never
    /// changed, permanently resolvable at the public verify endpoint (§2/§4) regardless of this
    /// row's later status.
    /// </summary>
    public string DigitalVerificationId { get; private init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset? ReadyAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    /// <summary>Set when a correction supersedes this (necessarily <see cref="GeneratedDocumentStatus.Ready"/>-at-the-time) document with a newly generated one (§4).</summary>
    public GeneratedDocumentId? SupersededByDocumentId { get; private set; }

    /// <summary>DOC-4: the bulk job this item was produced by, if generated via the async/bulk path rather than the synchronous one. <c>null</c> for a synchronously generated document.</summary>
    public Guid? BulkGenerationJobId { get; private init; }

    /// <summary>
    /// The caller-supplied field data this document was (or, if still Pending/retrying, will be)
    /// rendered from - kept alongside the claim row, not just passed transiently, specifically so
    /// DOC-15's async-retry fallback can re-render without the original synchronous caller still
    /// being involved (edge-cases.md's object-storage-outage-during-sync-receipt edge case: "falls
    /// back to an async retry and the calling module's transaction is unaffected").
    /// </summary>
    public string RenderDataJson { get; private init; } = "{}";

    public LanguageCode Language { get; private init; }

    /// <summary>Who to notify on completion (DOC-13) - <c>null</c> for a bulk-job item, whose completion is reported at the job level instead.</summary>
    public Guid? RequestedByUserId { get; private init; }

    /// <summary>DOC-3/edge-cases.md: the claim - reserves the natural key before any render/upload work starts. The only way a <see cref="GeneratedDocument"/> ever comes into existence.</summary>
    public static GeneratedDocument Claim(
        Guid ownerId,
        DocumentType documentType,
        Guid sourceReferenceId,
        DocumentTemplateId templateId,
        int templateVersion,
        string digitalVerificationId,
        string renderDataJson,
        LanguageCode language,
        DateTimeOffset now,
        Guid? requestedByUserId = null,
        Guid? bulkGenerationJobId = null)
    {
        return new GeneratedDocument
        {
            Id = GeneratedDocumentId.New(),
            OwnerId = ownerId,
            DocumentType = documentType,
            SourceReferenceId = sourceReferenceId,
            TemplateId = templateId,
            TemplateVersion = templateVersion,
            DigitalVerificationId = digitalVerificationId,
            RenderDataJson = renderDataJson,
            Language = language,
            RequestedByUserId = requestedByUserId,
            Status = GeneratedDocumentStatus.Pending,
            CreatedAt = now,
            BulkGenerationJobId = bulkGenerationJobId,
        };
    }

    /// <summary>
    /// First half of design-decisions.md's saga: the object-storage upload has already succeeded
    /// (outside any database transaction) - this attaches the (still-unverified) object key in its
    /// own transaction. The row stays <see cref="GeneratedDocumentStatus.Uploaded"/>, not yet
    /// <c>Ready</c>, until <see cref="MarkReady"/> confirms the checksum separately - so a crash
    /// between the two never leaves a row falsely claiming <c>Ready</c>.
    /// </summary>
    public Result AttachUploadedArtifact(string storageKey, string checksum, string mimeType, long sizeBytes)
    {
        if (Status is not (GeneratedDocumentStatus.Pending or GeneratedDocumentStatus.Uploaded))
        {
            return Result.Failure(Error.Conflict("generated_document.invalid_transition", $"Cannot attach an uploaded artifact to a document in status '{Status}'."));
        }

        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return Result.Failure(Error.Validation("generated_document.storage_key_required", "A storage key is required."));
        }

        if (string.IsNullOrWhiteSpace(checksum))
        {
            return Result.Failure(Error.Validation("generated_document.checksum_required", "A checksum is required."));
        }

        StorageKey = storageKey;
        Checksum = checksum;
        MimeType = mimeType;
        SizeBytes = sizeBytes;
        Status = GeneratedDocumentStatus.Uploaded;
        return Result.Success();
    }

    /// <summary>Second half of the saga: the checksum has now been independently verified - the row is retrievable and correct.</summary>
    public Result MarkReady(DateTimeOffset now)
    {
        if (Status != GeneratedDocumentStatus.Uploaded)
        {
            return Result.Failure(Error.Conflict("generated_document.invalid_transition", $"Cannot mark a document Ready from status '{Status}' - it must have an uploaded, unverified artifact first."));
        }

        Status = GeneratedDocumentStatus.Ready;
        ReadyAt = now;
        return Result.Success();
    }

    /// <summary>Render failure, upload failure, or a failed/unrecoverable checksum verification (edge-cases.md's object-storage/DB-ordering decision) - the natural key becomes reclaimable via <see cref="Reopen"/>.</summary>
    public Result MarkFailed()
    {
        if (Status is GeneratedDocumentStatus.Ready or GeneratedDocumentStatus.Revoked or GeneratedDocumentStatus.Superseded)
        {
            return Result.Failure(Error.Conflict("generated_document.invalid_transition", $"Cannot fail a document already in terminal-success status '{Status}'."));
        }

        Status = GeneratedDocumentStatus.Failed;
        StorageKey = null;
        Checksum = null;
        return Result.Success();
    }

    /// <summary>requirement-spec.md documents §8: a retried request against a natural key that previously failed must retry, not be permanently blocked by the unique constraint. Re-arms the same claim row rather than inserting a new one.</summary>
    public Result Reopen()
    {
        if (Status != GeneratedDocumentStatus.Failed)
        {
            return Result.Failure(Error.Conflict("generated_document.invalid_transition", $"Cannot reopen a document in status '{Status}' - only a Failed claim may be retried."));
        }

        Status = GeneratedDocumentStatus.Pending;
        return Result.Success();
    }

    /// <summary>edge-cases.md's revoke decision: only a <see cref="GeneratedDocumentStatus.Ready"/> document may be revoked - never Pending/Uploaded/non-existent/already-terminal.</summary>
    public Result Revoke(string reason, DateTimeOffset now)
    {
        if (Status != GeneratedDocumentStatus.Ready)
        {
            return Result.Failure(Error.Conflict("generated_document.revoke_requires_ready", $"Cannot revoke a document in status '{Status}' - only a Ready document may be revoked."));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure(Error.Validation("generated_document.revoke_reason_required", "A reason is required to revoke a document."));
        }

        Status = GeneratedDocumentStatus.Revoked;
        RevokedAt = now;
        RevokedReason = reason.Trim();
        return Result.Success();
    }

    /// <summary>requirement-spec.md documents §4: a correction creates a new GeneratedDocument and marks this one Superseded - never an in-place file replace.</summary>
    public Result Supersede(GeneratedDocumentId replacementId)
    {
        if (Status != GeneratedDocumentStatus.Ready)
        {
            return Result.Failure(Error.Conflict("generated_document.supersede_requires_ready", $"Cannot supersede a document in status '{Status}' - only a Ready document may be superseded."));
        }

        Status = GeneratedDocumentStatus.Superseded;
        SupersededByDocumentId = replacementId;
        return Result.Success();
    }
}
