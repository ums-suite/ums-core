namespace UMS.Modules.Content.Application.Abstractions;

/// <summary>Content's own local port, adapted onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c> in Infrastructure - mirrors every other module's own <c>INotificationRequestPublisher</c> exactly.</summary>
public interface INoticeNotificationPublisher
{
    public Task PublishAsync(NoticeNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>CNT-13: requirement-spec.md §3/§7 - urgent-notice fan-out per audience, one request per resolved recipient.</summary>
public sealed record NoticeNotificationRequest(string EventType, string SourceEntityId, Guid RecipientUserId, IReadOnlyDictionary<string, string?> MergeFields);
