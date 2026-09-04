namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>The payload <see cref="INotificationRequestPublisher"/> raises - see that interface's own remarks.</summary>
/// <param name="RecipientUserId">The owner who should be told their document is ready (or failed).</param>
/// <param name="EventType">One of "DocumentGenerated", "DocumentGenerationFailed", "BulkDocumentGenerationCompleted" (requirement-spec.md documents §6).</param>
/// <param name="SourceEntityId">The `GeneratedDocument`/`BulkGenerationJob` id this event concerns - Notifications' own dedupe key granularity (release/DEVELOPMENT_PLAN.md Flow #8, NTF-4) is keyed on this, not on the outbox message id, so a redelivered outbox message for the same document/job collapses into one notification.</param>
/// <param name="MergeFields">Merge-field data for Notifications' own bilingual template rendering (Flow #8, NTF-7) - e.g. <c>{"documentType":"transcript"}</c>. Notifications owns composing the actual subject/body text; this module supplies only the data.</param>
/// <param name="CorrelationId">Threaded end-to-end (ums-conventions.md, Observability).</param>
public sealed record NotificationRequest(Guid RecipientUserId, string EventType, string SourceEntityId, IReadOnlyDictionary<string, string> MergeFields, string CorrelationId);
