using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Domain.Events;

/// <summary>
/// requirement-spec.md §3 consumers: "ops alerting (non-paging channel ... unless the category is
/// OTP/security-critical)". design-decisions.md, "Dead-Letter Alerting Tier - Category-Conditional
/// Paging".
/// </summary>
public sealed record NotificationDeadLettered(
    Guid NotificationRequestId,
    Guid AttemptId,
    NotificationChannel Channel,
    NotificationCategory Category,
    DeadLetterReason Reason,
    Guid RecipientId,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    /// <summary>design-decisions.md: OTP/security-critical dead-letters page on-call directly; every other category notifies a non-paging channel first.</summary>
    public bool RequiresPaging => Category.IsExpeditedByDefault();
}
