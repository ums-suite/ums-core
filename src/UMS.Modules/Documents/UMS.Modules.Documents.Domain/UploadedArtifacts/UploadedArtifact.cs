using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Domain.UploadedArtifacts;

/// <summary>
/// requirement-spec.md documents §2/§3: a user-supplied file a calling module needs referenced but
/// never authors the content of (a Career <c>ResumeProfile</c>, an Alumni <c>JobApplication</c>'s
/// resume, a Learning <c>Submission</c>/<c>LectureMaterial</c>) - distinct from
/// <see cref="GeneratedDocuments.GeneratedDocument"/>, which is always system-rendered. Same
/// metadata-row-only, never-a-database-blob principle; no template, no render step. Immutable once
/// <see cref="UploadedArtifactStatus.Ready"/> - a replacement creates a new
/// <see cref="UploadedArtifact"/>, never an in-place overwrite of the object-storage key (§2).
///
/// <para>
/// <see cref="ArtifactType"/> is a free-form, caller-owned string rather than an enum: every
/// calling module named in §2 (Career, Alumni, Learning) is a not-yet-built module
/// (release/DEVELOPMENT_PLAN.md Flows #9/#13/#29/#30 land after Documents) - Documents does not
/// know their vocabulary in advance and must not guess it speculatively.
/// </para>
/// </summary>
public sealed class UploadedArtifact
{
    private UploadedArtifact()
    {
    }

    public UploadedArtifactId Id { get; private init; }

    public Guid OwnerId { get; private init; }

    public string ArtifactType { get; private init; } = string.Empty;

    public string MimeType { get; private init; } = string.Empty;

    public string StorageKey { get; private init; } = string.Empty;

    public string? Checksum { get; private set; }

    public long? SizeBytes { get; private set; }

    public UploadedArtifactStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private init; }

    public DateTimeOffset? ReadyAt { get; private set; }

    public string? FailureReason { get; private set; }

    /// <summary>Requests a presigned upload URL - the row starts life below <see cref="UploadedArtifactStatus.Ready"/> and is not yet a valid reference for any calling module (§2).</summary>
    public static Result<UploadedArtifact> RequestUpload(Guid ownerId, string artifactType, string mimeType, string storageKey, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(artifactType))
        {
            return Error.Validation("uploaded_artifact.artifact_type_required", "An artifact type is required.");
        }

        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return Error.Validation("uploaded_artifact.mime_type_required", "A MIME type is required.");
        }

        return new UploadedArtifact
        {
            Id = UploadedArtifactId.New(),
            OwnerId = ownerId,
            ArtifactType = artifactType.Trim(),
            MimeType = mimeType.Trim(),
            StorageKey = storageKey,
            Status = UploadedArtifactStatus.PendingUpload,
            RequestedAt = now,
        };
    }

    /// <summary>DOC's upload-confirm step: existence + checksum verified - only now does this row become a valid reference for any calling module (§2).</summary>
    public Result Confirm(string checksum, long sizeBytes, DateTimeOffset now)
    {
        if (Status != UploadedArtifactStatus.PendingUpload)
        {
            return Result.Failure(Error.Conflict("uploaded_artifact.invalid_transition", $"Cannot confirm an artifact in status '{Status}'."));
        }

        Checksum = checksum;
        SizeBytes = sizeBytes;
        Status = UploadedArtifactStatus.Ready;
        ReadyAt = now;
        return Result.Success();
    }

    public Result MarkFailed(string reason)
    {
        if (Status != UploadedArtifactStatus.PendingUpload)
        {
            return Result.Failure(Error.Conflict("uploaded_artifact.invalid_transition", $"Cannot fail an artifact in status '{Status}'."));
        }

        Status = UploadedArtifactStatus.Failed;
        FailureReason = reason;
        return Result.Success();
    }
}
