using UMS.Modules.Documents.Domain.GeneratedDocuments;

namespace UMS.Modules.Documents.Application.Generation;

public enum PipelineOutcome
{
    /// <summary>Render + upload + checksum verification all succeeded - the row is now <see cref="GeneratedDocumentStatus.Ready"/>.</summary>
    Ready,

    /// <summary>
    /// design-decisions.md's saga-style flow / edge-cases.md's object-storage-outage decision: the
    /// object-storage call itself failed (or a subsequent checksum mismatch was detected) - the
    /// claim row is left resumable (still <see cref="GeneratedDocumentStatus.Pending"/>, or reset
    /// to it) rather than permanently <see cref="GeneratedDocumentStatus.Failed"/>, and a retry
    /// outbox message has been enqueued. Never thrown as an exception - callers on the synchronous
    /// path must be able to return gracefully to a caller whose own transaction (e.g. Finance's
    /// already-committed Payment) must not be blocked or rolled back (§8/§9).
    /// </summary>
    StorageOutageRetryQueued,

    /// <summary>A non-storage failure (a render defect, a checksum mismatch after a successful-looking upload) - the row is <see cref="GeneratedDocumentStatus.Failed"/> and NOT automatically retried; the natural key may still be reopened by a fresh caller request.</summary>
    Failed,
}
