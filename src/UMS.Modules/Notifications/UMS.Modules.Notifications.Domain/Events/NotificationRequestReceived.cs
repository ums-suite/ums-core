using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Domain.Events;

/// <summary>requirement-spec.md §3: "internal, drives fan-out." Raised once, at <see cref="Requests.NotificationRequest.Create"/> time.</summary>
public sealed record NotificationRequestReceived(
    Guid NotificationRequestId,
    string SourceModule,
    string EventType,
    Guid RecipientId,
    NotificationCategory Category,
    DateTimeOffset OccurredAt) : IDomainEvent;
