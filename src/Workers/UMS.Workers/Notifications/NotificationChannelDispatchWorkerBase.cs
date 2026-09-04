using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Dispatch;
using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Workers.Notifications;

/// <summary>
/// NTF-13's per-channel retry/dead-letter dispatch loop and NTF-16's bulk/backpressure-aware
/// prioritized dispatch (design-decisions.md "Bulk-Burst Prioritized Dispatch - Three-Tier Queue
/// Model") - one instance per <see cref="NotificationChannel"/> (see each channel's own thin
/// subclass, registered once each in <c>Program.cs</c>), each running its own independent poll loop
/// so a channel with a backed-up queue (edge-cases.md's "SMS provider outage") never delays another
/// channel's dispatch (ADR-0009's per-channel isolation).
///
/// <para>
/// <b>Three-tier mechanism, concretely:</b> every poll tick first claims and processes a batch from
/// the Expedited+Standard tiers; the Bulk tier (large batch jobs like a result-publication run) is
/// only given worker capacity on a tick where Expedited/Standard had nothing to claim - this is what
/// makes Bulk-tier dispatch throttled against Standard's own available capacity without a separate
/// physical queue/worker pool (design-decisions.md's own "within ADR-0014's single shared Outbox/
/// worker infrastructure" framing), at the cost of Bulk throughput being lower under any sustained
/// Expedited/Standard load - the accepted trade-off that same decision names explicitly.
/// </para>
/// </summary>
public abstract class NotificationChannelDispatchWorkerBase(NotificationChannel channel, IServiceScopeFactory scopeFactory, ILogger logger) : BackgroundService
{
    private const int ExpeditedStandardBatchSize = 25;
    private const int BulkBatchSize = 50;
    private static readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);
    private static readonly IReadOnlyList<NotificationPriority> _expeditedStandardTiers = [NotificationPriority.Expedited, NotificationPriority.Standard];
    private static readonly IReadOnlyList<NotificationPriority> _bulkTier = [NotificationPriority.Bulk];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var claimedHigherTier = await ProcessTierAsync(_expeditedStandardTiers, ExpeditedStandardBatchSize, stoppingToken).ConfigureAwait(false);
                if (!claimedHigherTier)
                {
                    await ProcessTierAsync(_bulkTier, BulkBatchSize, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Notification dispatch worker for channel {Channel} failed unexpectedly this tick.", channel);
            }

            await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> ProcessTierAsync(IReadOnlyList<NotificationPriority> tiers, int batchSize, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var attempts = scope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>();
        var claimed = await attempts.ClaimBatchAsync(channel, tiers, batchSize, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (claimed.Count == 0)
        {
            return false;
        }

        var dispatchService = scope.ServiceProvider.GetRequiredService<NotificationDispatchService>();
        foreach (var item in claimed)
        {
            try
            {
                await dispatchService.ProcessClaimedAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One attempt's own unexpected failure never aborts the rest of the claimed batch -
                // ADR-0009's per-channel/per-request independence applies within a channel's own
                // batch too, not only across channels.
                logger.LogError(ex, "Failed to process claimed delivery attempt {AttemptId} on channel {Channel}.", item.AttemptId, channel);
            }
        }

        return true;
    }
}
