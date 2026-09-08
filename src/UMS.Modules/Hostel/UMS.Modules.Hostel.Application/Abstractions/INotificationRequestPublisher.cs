namespace UMS.Modules.Hostel.Application.Abstractions;

/// <summary>Hostel's own local port, adapted onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c> in Infrastructure - mirrors every other module's own <c>INotificationRequestPublisher</c> exactly.</summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(HostelNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>requirement-spec.md §7/§3: application submitted, allocation approved/bed allocated, fee due, check-in reminder, complaint resolved.</summary>
public sealed record HostelNotificationRequest(string EventType, string SourceEntityId, Guid RecipientUserId, IReadOnlyDictionary<string, string?> MergeFields);
