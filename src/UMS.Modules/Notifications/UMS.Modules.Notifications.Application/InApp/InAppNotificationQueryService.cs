using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Application.InApp;

/// <summary>NTF-12: the in-app notification center's own read API - <c>GET /notifications/me</c>, <c>GET /notifications/me/unread-count</c>, <c>PATCH /notifications/me/{id}/read</c>, <c>PATCH /notifications/me/read-all</c>.</summary>
public sealed class InAppNotificationQueryService(INotificationDeliveryAttemptRepository attempts, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<IReadOnlyList<NotificationDeliveryAttemptDto>> ListAsync(Guid recipientId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var rows = await attempts.ListInAppForRecipientAsync(recipientId, skip, take, cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(NotificationDeliveryAttemptDto.FromDomain)];
    }

    public Task<int> UnreadCountAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        attempts.CountUnreadInAppForRecipientAsync(recipientId, cancellationToken);

    public async Task<Result> MarkReadAsync(Guid recipientId, Guid attemptId, CancellationToken cancellationToken = default)
    {
        var attempt = await attempts.GetInAppForRecipientAsync(recipientId, new NotificationDeliveryAttemptId(attemptId), cancellationToken).ConfigureAwait(false);
        if (attempt is null)
        {
            return Result.Failure(Error.NotFound("in_app_notification.not_found", $"No in-app notification exists with id '{attemptId}' for the current recipient."));
        }

        attempt.MarkRead(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task MarkAllReadAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // A small, bounded scan of this recipient's own rows - the notification center's own
        // history is retention-bounded (§8 edge case), so this never approaches an unbounded table
        // scan in practice.
        const int batchSize = 500;
        int skip = 0;
        IReadOnlyList<NotificationDeliveryAttempt> page;
        do
        {
            page = await attempts.ListInAppForRecipientAsync(recipientId, skip, batchSize, cancellationToken).ConfigureAwait(false);
            var unread = page.Where(a => a.ReadAt is null).ToList();
            foreach (var attempt in unread)
            {
                attempt.MarkRead(now);
            }

            if (unread.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }

            skip += batchSize;
        }
        while (page.Count == batchSize);
    }
}
