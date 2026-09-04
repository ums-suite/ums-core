using Microsoft.EntityFrameworkCore;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Repositories;

/// <summary>
/// NTF-13/NTF-16's claim query is the heart of this repository - see
/// <see cref="ClaimBatchAsync"/>'s own remarks for why it is a single, self-contained
/// <c>SELECT ... FOR UPDATE SKIP LOCKED</c>-backed atomic UPDATE, not a check-then-update pair.
/// </summary>
internal sealed class NotificationDeliveryAttemptRepository(NotificationsDbContext context) : INotificationDeliveryAttemptRepository
{
    public Task<NotificationDeliveryAttempt?> GetByIdAsync(NotificationDeliveryAttemptId id, CancellationToken cancellationToken = default) =>
        context.DeliveryAttempts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<NotificationDeliveryAttempt?> GetByProviderMessageIdAsync(NotificationChannel channel, string providerMessageId, CancellationToken cancellationToken = default) =>
        context.DeliveryAttempts.FirstOrDefaultAsync(a => a.Channel == channel && a.ProviderMessageId == providerMessageId, cancellationToken);

    /// <summary>
    /// design-decisions.md, "Retry/Backoff Design Per Channel": a genuine row-lock-guarded claim,
    /// not an application-level check-then-update - <c>FOR UPDATE OF a SKIP LOCKED</c> lets several
    /// dispatch worker instances (one per channel, per NTF-16's tiering) safely compete for the same
    /// table without either double-claiming a row or blocking on rows another worker already holds.
    /// The claim (this method) and the slow provider call that follows it (see
    /// <c>NotificationDispatchService</c>) are deliberately two separate transactions - holding this
    /// row lock for the duration of an external HTTP call would serialize dispatch throughput on
    /// database lock contention instead of actual provider latency.
    /// </summary>
    public async Task<IReadOnlyList<ClaimedAttempt>> ClaimBatchAsync(NotificationChannel channel, IReadOnlyList<NotificationPriority> priorities, int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var staleCutoff = now - NotificationDeliveryAttempt.InFlightTimeout;
        var priorityValues = priorities.Select(p => (int)p).ToArray();
        var channelValue = (int)channel;
        var pendingValue = (int)DeliveryAttemptStatus.Pending;
        var retryingValue = (int)DeliveryAttemptStatus.Retrying;
        var inFlightValue = (int)DeliveryAttemptStatus.InFlight;

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var claimed = await context.Database.SqlQuery<ClaimedAttemptRow>($"""
            WITH candidates AS (
                SELECT a.id
                FROM notifications.notification_delivery_attempts a
                JOIN notifications.notification_requests r ON r.id = a.notification_request_id
                WHERE a.channel = {channelValue}
                  AND r.priority = ANY({priorityValues})
                  AND (
                        a.status = {pendingValue}
                     OR (a.status = {retryingValue} AND (a.next_attempt_at IS NULL OR a.next_attempt_at <= {now}))
                     OR (a.status = {inFlightValue} AND a.in_flight_since < {staleCutoff})
                  )
                ORDER BY r.priority, a.created_at
                LIMIT {batchSize}
                FOR UPDATE OF a SKIP LOCKED
            )
            UPDATE notifications.notification_delivery_attempts a
            SET status = {inFlightValue}, in_flight_since = {now}, updated_at = {now}
            FROM candidates
            WHERE a.id = candidates.id
            RETURNING a.id AS "AttemptId", a.notification_request_id AS "NotificationRequestId", a.attempt_count AS "AttemptCount"
            """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return [.. claimed.Select(row => new ClaimedAttempt(row.AttemptId, row.NotificationRequestId, row.AttemptCount))];
    }

    public async Task<NotificationDeliveryAttempt?> GetInAppForRecipientAsync(Guid recipientId, NotificationDeliveryAttemptId id, CancellationToken cancellationToken = default) =>
        await context.DeliveryAttempts
            .Join(context.NotificationRequests, a => a.NotificationRequestId, r => r.Id, (a, r) => new { Attempt = a, r.RecipientId })
            .Where(x => x.Attempt.Id == id && x.Attempt.Channel == NotificationChannel.InApp && x.RecipientId == recipientId)
            .Select(x => x.Attempt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NotificationDeliveryAttempt>> ListInAppForRecipientAsync(Guid recipientId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.DeliveryAttempts
            .Join(context.NotificationRequests, a => a.NotificationRequestId, r => r.Id, (a, r) => new { Attempt = a, r.RecipientId })
            .Where(x => x.Attempt.Channel == NotificationChannel.InApp && x.RecipientId == recipientId)
            .OrderByDescending(x => x.Attempt.CreatedAt)
            .Skip(skip)
            .Take(take)
            .Select(x => x.Attempt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<int> CountUnreadInAppForRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        await context.DeliveryAttempts
            .Join(context.NotificationRequests, a => a.NotificationRequestId, r => r.Id, (a, r) => new { Attempt = a, r.RecipientId })
            .Where(x => x.Attempt.Channel == NotificationChannel.InApp && x.RecipientId == recipientId && x.Attempt.ReadAt == null && x.Attempt.Status == DeliveryAttemptStatus.Delivered)
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NotificationDeliveryAttempt>> ListDeadLettersAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.DeliveryAttempts
            .Where(a => a.Status == DeliveryAttemptStatus.DeadLettered)
            .OrderByDescending(a => a.UpdatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByRequestIdAsync(NotificationRequestId requestId, CancellationToken cancellationToken = default) =>
        await context.DeliveryAttempts
            .Where(a => a.NotificationRequestId == requestId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private sealed record ClaimedAttemptRow(Guid AttemptId, Guid NotificationRequestId, int AttemptCount);
}
