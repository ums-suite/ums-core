using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Application.Permissions;
using UMS.Modules.Content.Domain.Events;
using UMS.Shared.Identity;

namespace UMS.Workers.Content;

/// <summary>
/// CNT-13: requirement-spec.md §3/§7 - on <see cref="NoticePublished"/>, if the Notice has an urgent
/// flag, raise a <see cref="INoticeNotificationPublisher"/> request for email/SMS/push fan-out per
/// audience. Non-urgent notices are marked processed and skipped entirely - no fan-out.
///
/// <para>
/// <b>Recipient resolution / known gap:</b> when the urgent Notice carries an
/// <c>OrganizationNodeId</c> (a Department-scoped notice), recipients are resolved via
/// <see cref="IScopeGrantDirectory.GetUserIdsWithPermissionAtScopeAsync"/> - every user currently
/// holding <see cref="ContentPermissions.NoticeRead"/> at that scope. A university-wide urgent
/// notice (no <c>OrganizationNodeId</c>) has no such directory query to enumerate "everyone holding
/// this permission platform-wide" - <see cref="IScopeGrantDirectory"/> is deliberately exact-scope,
/// not a broadcast primitive (see its own remarks) - so that case is logged and skipped rather than
/// guessed at; a genuine platform-wide broadcast mechanism is a documented gap for a future ticket,
/// not built here.
/// </para>
/// </summary>
public sealed class NoticeNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<NoticeNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Content notice notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var publisher = scope.ServiceProvider.GetRequiredService<INoticeNotificationPublisher>();
        var scopeGrants = scope.ServiceProvider.GetRequiredService<IScopeGrantDirectory>();

        var messages = await outbox.GetUnprocessedAsync(nameof(NoticePublished), BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var evt = JsonSerializer.Deserialize<NoticePublished>(message.PayloadJson) ?? throw new InvalidOperationException("Empty NoticePublished payload.");
                if (!evt.IsUrgent)
                {
                    await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (evt.OrganizationNodeId is { } organizationNodeId)
                {
                    var recipients = await scopeGrants.GetUserIdsWithPermissionAtScopeAsync(ContentPermissions.NoticeRead, organizationNodeId, cancellationToken).ConfigureAwait(false);
                    foreach (var recipientUserId in recipients)
                    {
                        await publisher.PublishAsync(new NoticeNotificationRequest(nameof(NoticePublished), evt.NoticeId.ToString(), recipientUserId, new Dictionary<string, string?> { ["audience"] = evt.Audience.ToString() }), cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    logger.LogWarning("Content notice notification relay: urgent Notice {NoticeId} has no OrganizationNodeId to resolve a bounded recipient list against - a platform-wide broadcast primitive is a documented gap, skipping fan-out for this notice.", evt.NoticeId);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Content notice notification relay: publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
