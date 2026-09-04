namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>The payload <see cref="INotificationRequestPublisher"/> raises - see that interface's own remarks.</summary>
/// <param name="RecipientUserId">The owner who should be told their document is ready (or failed).</param>
/// <param name="EventType">One of "DocumentGenerated", "DocumentGenerationFailed", "BulkDocumentGenerationCompleted" (requirement-spec.md documents §6).</param>
/// <param name="Subject">A short, human-readable summary - the real Notifications module will template this per-channel once it exists.</param>
/// <param name="CorrelationId">Threaded end-to-end (ums-conventions.md, Observability).</param>
public sealed record NotificationRequest(Guid RecipientUserId, string EventType, string Subject, string CorrelationId);
