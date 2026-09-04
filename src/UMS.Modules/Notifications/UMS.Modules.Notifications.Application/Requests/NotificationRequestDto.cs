using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Application.Requests;

public sealed record NotificationRequestDto(
    Guid Id,
    string SourceModule,
    string EventType,
    string SourceEntityId,
    Guid RecipientId,
    NotificationCategory Category,
    NotificationPriority Priority,
    DateTimeOffset CreatedAt,
    IReadOnlyList<NotificationDeliveryAttemptDto> Attempts)
{
    public static NotificationRequestDto FromDomain(NotificationRequest request) => new(
        request.Id.Value,
        request.SourceModule,
        request.EventType,
        request.SourceEntityId,
        request.RecipientId,
        request.Category,
        request.Priority,
        request.CreatedAt,
        [.. request.Attempts.Select(NotificationDeliveryAttemptDto.FromDomain)]);
}

public sealed record NotificationDeliveryAttemptDto(
    Guid Id,
    Guid NotificationRequestId,
    NotificationChannel Channel,
    DeliveryAttemptStatus Status,
    int AttemptCount,
    DeadLetterReason? DeadLetterReason,
    string? LastError,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? ReadAt,
    string? RenderedSubject,
    string? RenderedBody,
    string? RenderedDeepLink,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static NotificationDeliveryAttemptDto FromDomain(NotificationDeliveryAttempt attempt) => new(
        attempt.Id.Value,
        attempt.NotificationRequestId.Value,
        attempt.Channel,
        attempt.Status,
        attempt.AttemptCount,
        attempt.DeadLetterReason,
        attempt.LastError,
        attempt.DeliveredAt,
        attempt.ReadAt,
        attempt.RenderedSubject,
        attempt.RenderedBody,
        attempt.RenderedDeepLink,
        attempt.CreatedAt,
        attempt.UpdatedAt);
}
