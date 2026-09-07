namespace UMS.Modules.Admission.Domain.Applications;

public enum ApplicationDocumentStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>
/// requirement-spec.md §2 Draft: "uploads required ApplicationDocuments"; §8 edge case "Document
/// rejected during verification pre-lock" - reopens only THIS document for re-upload, never the
/// whole (possibly already-Locked) <see cref="Application"/>.
/// </summary>
public sealed class ApplicationDocument
{
    internal ApplicationDocument(Guid id, string documentType, string fileReference, DateTimeOffset now)
    {
        Id = id;
        DocumentType = documentType;
        FileReference = fileReference;
        Status = ApplicationDocumentStatus.Pending;
        UploadedAt = now;
    }

    private ApplicationDocument()
    {
    }

    public Guid Id { get; private set; }

    public string DocumentType { get; private set; } = string.Empty;

    public string FileReference { get; private set; } = string.Empty;

    public ApplicationDocumentStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public Guid? ReviewedByUserId { get; private set; }

    internal void Replace(string fileReference, DateTimeOffset now)
    {
        FileReference = fileReference;
        Status = ApplicationDocumentStatus.Pending;
        RejectionReason = null;
        UploadedAt = now;
        ReviewedAt = null;
        ReviewedByUserId = null;
    }

    internal void Approve(Guid reviewedByUserId, DateTimeOffset now)
    {
        Status = ApplicationDocumentStatus.Approved;
        RejectionReason = null;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = now;
    }

    /// <summary>§8 edge case: "the specific document reopens for re-upload without unlocking the rest of the Application" - reflected here simply by this method not touching the owning Application's own <see cref="Application.Status"/> at all.</summary>
    internal void RequestResubmission(string reason, Guid reviewedByUserId, DateTimeOffset now)
    {
        Status = ApplicationDocumentStatus.Rejected;
        RejectionReason = reason;
        ReviewedByUserId = reviewedByUserId;
        ReviewedAt = now;
    }
}
