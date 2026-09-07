namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>
/// This module's own port over Notifications' shared
/// <c>UMS.Shared.Notifications.INotificationRequestIntake</c> contract (ADR-0009: "never a direct
/// email/SMS/push call"), mirroring Documents'/Learning's own <c>INotificationRequestPublisher</c>
/// exactly. Called from <c>UMS.Workers</c>' Finance relay, never inline in a request path - a
/// Notifications outage must never fail or re-queue the underlying Payment mutation that already
/// committed.
/// </summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(FinanceNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>One fan-out request. <paramref name="DedupeKey"/> is the originating outbox message id, so a relay retry is a no-op at Notifications' own natural-key dedupe rather than a duplicate send.</summary>
public sealed record FinanceNotificationRequest(
    Guid RecipientUserId,
    string EventType,
    string SourceEntityId,
    IReadOnlyDictionary<string, string> MergeFields,
    string DedupeKey);
