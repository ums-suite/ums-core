using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Domain.Events;

/// <summary>requirement-spec.md §3 consumers: "Reporting (delivery-rate metrics), Audit (for OTP/security/payment/result categories only)".</summary>
public sealed record NotificationDeliveryAttemptSucceeded(
    Guid NotificationRequestId,
    Guid AttemptId,
    NotificationChannel Channel,
    Guid RecipientId,
    NotificationCategory Category,
    DateTimeOffset OccurredAt) : IDomainEvent;
