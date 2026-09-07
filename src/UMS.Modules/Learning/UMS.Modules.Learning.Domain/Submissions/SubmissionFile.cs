using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.Submissions;

/// <summary>
/// One file attached to a <see cref="Submission"/>, held purely as a foreign reference to
/// Documents' own <c>UploadedArtifact</c> (requirement-spec.md learning §7.1: "Learning stores the
/// returned <c>artifactId</c> as its own foreign reference"). There is deliberately no storage key,
/// no bucket, no bytes, and no second object-storage integration anywhere in this module -
/// design-decisions.md's "Raw File Storage via Documents' Object-Storage Integration" rules that
/// out explicitly.
/// </summary>
public sealed record SubmissionFile
{
    private SubmissionFile(Guid artifactId, string fileName, string mimeType)
    {
        ArtifactId = artifactId;
        FileName = fileName;
        MimeType = mimeType;
    }

    /// <summary>Documents' own <c>UploadedArtifactId</c>, already confirmed <c>Ready</c> before it reaches here.</summary>
    public Guid ArtifactId { get; }

    public string FileName { get; }

    public string MimeType { get; }

    public static Result<SubmissionFile> Create(Guid artifactId, string fileName, string mimeType)
    {
        if (artifactId == Guid.Empty)
        {
            return Error.Validation("submission_file.artifact_required", "A SubmissionFile must reference a confirmed Documents UploadedArtifact.");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return Error.Validation("submission_file.file_name_required", "A SubmissionFile must carry the original file name.");
        }

        return string.IsNullOrWhiteSpace(mimeType)
            ? Error.Validation("submission_file.mime_type_required", "A SubmissionFile must carry its MIME type.")
            : new SubmissionFile(artifactId, fileName.Trim(), mimeType.Trim());
    }
}
