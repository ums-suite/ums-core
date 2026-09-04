using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Application.Admin;

public sealed record DeadLetterDto(
    Guid AttemptId,
    Guid NotificationRequestId,
    string EventType,
    NotificationCategory? Category,
    NotificationChannel Channel,
    DeadLetterReason? Reason,
    string? LastError,
    int AttemptCount,
    DateTimeOffset UpdatedAt);
