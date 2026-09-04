namespace UMS.Modules.Faculty.Application.Abstractions;

/// <summary>
/// FAC-12: Faculty's own local port onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c>
/// (requirement-spec.md faculty §7 Notifications bullet) - mirrors Documents' own
/// <c>INotificationRequestPublisher</c>/<c>NotificationRequestIntakeAdapter</c> pattern exactly.
/// Called from Faculty's own outbox relay worker (<c>UMS.Workers</c>), never from a synchronous
/// request path - a Notifications outage must never block a LeaveRequest decision from committing.
/// </summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Faculty's own fan-out request, exactly as it calls <see cref="INotificationRequestPublisher.PublishAsync"/>.</summary>
/// <param name="RecipientUserId">The Identity UserId to notify.</param>
/// <param name="EventType">Faculty's own event name, e.g. <c>"LeaveApproved"</c>.</param>
/// <param name="SourceEntityId">The LeaveRequest id this notification concerns.</param>
/// <param name="MergeFields">Template merge-field data.</param>
/// <param name="DedupeKey">The outbox message id - Notifications' own dedup key (NTF-3).</param>
public sealed record NotificationRequest(
    Guid RecipientUserId,
    string EventType,
    string SourceEntityId,
    IReadOnlyDictionary<string, string> MergeFields,
    string DedupeKey);
