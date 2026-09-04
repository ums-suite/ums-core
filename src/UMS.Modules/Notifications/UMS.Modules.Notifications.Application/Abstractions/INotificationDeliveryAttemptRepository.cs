using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Application.Abstractions;

public interface INotificationDeliveryAttemptRepository
{
    public Task<NotificationDeliveryAttempt?> GetByIdAsync(NotificationDeliveryAttemptId id, CancellationToken cancellationToken = default);

    /// <summary>NTF-12's mark-read ownership check: returns the attempt only if it is an <see cref="NotificationChannel.InApp"/> row belonging to <paramref name="recipientId"/>'s own <c>NotificationRequest</c> - never another recipient's.</summary>
    public Task<NotificationDeliveryAttempt?> GetInAppForRecipientAsync(Guid recipientId, NotificationDeliveryAttemptId id, CancellationToken cancellationToken = default);

    public Task<NotificationDeliveryAttempt?> GetByProviderMessageIdAsync(NotificationChannel channel, string providerMessageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically claims up to <paramref name="batchSize"/> attempts on <paramref name="channel"/>
    /// whose priority is in <paramref name="priorities"/> and are currently eligible for dispatch
    /// (design-decisions.md "Retry/Backoff Design Per Channel": a genuine <c>SELECT ... FOR UPDATE
    /// SKIP LOCKED</c>-guarded claim, not an application-level check-then-update), transitioning
    /// each claimed row to <see cref="DeliveryAttemptStatus.InFlight"/> in the same statement.
    /// </summary>
    public Task<IReadOnlyList<ClaimedAttempt>> ClaimBatchAsync(NotificationChannel channel, IReadOnlyList<NotificationPriority> priorities, int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListInAppForRecipientAsync(Guid recipientId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountUnreadInAppForRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListDeadLettersAsync(int skip, int take, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByRequestIdAsync(NotificationRequestId requestId, CancellationToken cancellationToken = default);
}
