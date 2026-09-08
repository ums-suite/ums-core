namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>Library's own local port, adapted onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c> in Infrastructure - mirrors every other module's own <c>INotificationRequestPublisher</c> exactly.</summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(LibraryNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>requirement-spec.md §7/§3: due-date reminder, overdue notice, reservation-available notice, fine due.</summary>
public sealed record LibraryNotificationRequest(string EventType, string SourceEntityId, Guid RecipientUserId, IReadOnlyDictionary<string, string?> MergeFields);
