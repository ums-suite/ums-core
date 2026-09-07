namespace UMS.Modules.Admission.Application.Abstractions;

/// <summary>This module's own port over Notifications' shared <c>UMS.Shared.Notifications.INotificationRequestIntake</c> contract (ADR-0009), mirroring Finance's/Learning's own <c>INotificationRequestPublisher</c> exactly. Called only from <c>UMS.Workers</c>' Admission relay, never inline in a request path.</summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(AdmissionNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <param name="DedupeKey">The originating outbox message id, so a relay retry is a no-op at Notifications' own natural-key dedupe rather than a duplicate send.</param>
public sealed record AdmissionNotificationRequest(
    Guid RecipientUserId,
    string EventType,
    string SourceEntityId,
    IReadOnlyDictionary<string, string> MergeFields,
    string DedupeKey);
