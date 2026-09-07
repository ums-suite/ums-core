using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Documents;

/// <summary>
/// LRN-5/LRN-13 (release/DEVELOPMENT_PLAN.md Flow #13): lets another module broker a presigned
/// direct-to-object-storage upload through Documents' own <c>UploadedArtifact</c> pipeline
/// (documents requirement-spec.md §2 "Uploaded Artifact Storage"), storing only the returned
/// <c>artifactId</c> as a foreign reference - never standing up a second, parallel storage
/// mechanism (learning design-decisions.md, "Raw File Storage via Documents' Object-Storage
/// Integration").
///
/// <para>
/// Mirrors <see cref="IDocumentGenerationRequester"/>'s exact pattern: living in
/// <c>UMS.Shared.Documents</c>, not <c>UMS.Modules.Documents.*</c>, is what lets a calling module
/// request an upload slot without a forbidden dependency on Documents' Domain/Application/
/// Infrastructure internals (module-boundaries.md, ADR-0002). Documents' own Infrastructure layer
/// registers the one real implementation, delegating to its <c>UploadedArtifactService</c>.
/// </para>
///
/// <para>
/// <b>Why this exists at all, rather than a Learning caller using Documents' HTTP endpoints:</b>
/// <c>POST /api/v1/documents/uploads</c> and <c>.../confirm</c> are gated by
/// <c>document.document.generate</c> - a Registrar/back-office capability a Student uploading their
/// own assignment file will never hold. Adding an in-process contract (rather than loosening that
/// endpoint's permission, or adding a second HTTP surface) is exactly the arc Student's own Flow
/// #11 took for <see cref="IDocumentGenerationRequester"/> and
/// <c>UMS.Shared.Identity.IUserProvisioner</c>: a new shared contract, zero change to the owning
/// module's own HTTP surface or permission model. Authorization for the upload therefore belongs to
/// the CALLING module (Learning gates it behind its own submission-window/enrollment checks), not
/// to Documents - the same division documents requirement-spec.md §1 already draws for
/// virus-scanning ("owned wherever the upload endpoint lives, not by Documents' own rendering
/// path").
/// </para>
/// </summary>
public interface IUploadedArtifactRequester
{
    /// <summary>
    /// Registers a new <c>UploadedArtifact</c> row in <c>PendingUpload</c> and returns a
    /// short-lived presigned URL the client PUTs the bytes to directly (bypassing both modules' API
    /// tiers - required for the multi-hundred-MB lecture video case, learning requirement-spec.md
    /// §5). The returned artifact is NOT yet a valid reference until <see cref="ConfirmAsync"/>
    /// succeeds.
    /// </summary>
    public Task<Result<UploadedArtifactSlot>> RequestUploadAsync(RequestUploadedArtifactCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Documents' confirm step: verifies the object actually landed at the expected storage key and
    /// records its checksum/size, transitioning the artifact to <c>Ready</c> (or <c>Failed</c> if
    /// nothing is there). Returns the resulting status rather than throwing - a caller decides for
    /// itself whether a non-<c>Ready</c> outcome blocks its own operation.
    /// </summary>
    public Task<Result<UploadedArtifactReference>> ConfirmAsync(Guid artifactId, CancellationToken cancellationToken = default);
}

/// <summary>One caller's request for an upload slot, exactly as it calls <see cref="IUploadedArtifactRequester.RequestUploadAsync"/>.</summary>
/// <param name="OwnerId">The Identity <c>User</c> the uploaded file belongs to - Documents scopes its own owner-or-<c>document.document.read</c> read check to this.</param>
/// <param name="ArtifactType">The calling module's own vocabulary for what this file is, e.g. <c>"LearningSubmission"</c> / <c>"LectureMaterial"</c>. Free-form by construction - Documents deliberately does not own a shared enum of other modules' artifact kinds (see <c>UploadedArtifact</c>'s own remarks).</param>
/// <param name="MimeType">The content type the presigned URL is issued for; the client must upload with a matching <c>Content-Type</c>.</param>
public sealed record RequestUploadedArtifactCommand(Guid OwnerId, string ArtifactType, string MimeType);

/// <summary>The presigned upload slot Documents issues - <see cref="UploadUrl"/> is short-lived and single-purpose.</summary>
public sealed record UploadedArtifactSlot(Guid ArtifactId, string Status, string? UploadUrl);

/// <summary>An <c>UploadedArtifact</c>'s post-confirm state, as the calling module records it against its own entity.</summary>
public sealed record UploadedArtifactReference(Guid ArtifactId, string Status, string MimeType, long? SizeBytes);
