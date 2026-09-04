using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Domain.Events;

/// <summary>requirement-spec.md §3 - one transient-failure attempt, not yet dead-lettered.</summary>
public sealed record NotificationDeliveryAttemptFailed(
    Guid NotificationRequestId,
    Guid AttemptId,
    NotificationChannel Channel,
    string Error,
    int AttemptCount,
    DateTimeOffset OccurredAt) : IDomainEvent;
