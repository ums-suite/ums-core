using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.Events;
using UMS.Modules.Research.Domain.Grants;
using UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

namespace UMS.Workers.Research;

/// <summary>
/// requirement-spec.md §7/§11: fans <c>GrantFunded</c>/<c>GrantClosed</c>/<c>GrantReported</c> out to
/// every investigator (PI and Co-Is alike), <c>GrantPiReassignmentRequired</c> to Admin/Research-Office,
/// and <c>InstitutionalRepositoryEntryEmbargoLifted</c> to the depositor/supervising FacultyMember -
/// mirrors Content's own <c>NoticeNotificationRelayWorker</c> shape.
///
/// <para>
/// <b>Known, documented gap</b> - <c>GrantPiReassignmentRequired</c>'s "Admin/Research-Office alert"
/// has no bounded recipient list to resolve: unlike Content's own OrganizationNodeId-scoped urgent
/// Notice, a Grant carries no Organization scope to key an <c>IScopeGrantDirectory</c> lookup against,
/// and that directory is deliberately exact-scope, never a platform-wide broadcast primitive (see
/// <c>NoticeNotificationRelayWorker</c>'s own remarks for the identical situation). Logged and
/// skipped rather than guessed at - a genuine broadcast mechanism is a future-ticket gap, not solved
/// here.
/// </para>
/// </summary>
public sealed class ResearchNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<ResearchNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string GrantFundedEventType = typeof(GrantFunded).Name;
    private static readonly string GrantClosedEventType = typeof(GrantClosed).Name;
    private static readonly string GrantReportedEventType = typeof(GrantReported).Name;
    private static readonly string GrantPiReassignmentRequiredEventType = typeof(GrantPiReassignmentRequired).Name;
    private static readonly string EmbargoLiftedEventType = typeof(InstitutionalRepositoryEntryEmbargoLifted).Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessGrantLifecycleAsync(GrantFundedEventType, stoppingToken).ConfigureAwait(false);
                await ProcessGrantLifecycleAsync(GrantClosedEventType, stoppingToken).ConfigureAwait(false);
                await ProcessGrantLifecycleAsync(GrantReportedEventType, stoppingToken).ConfigureAwait(false);
                await ProcessPiReassignmentRequiredAsync(stoppingToken).ConfigureAwait(false);
                await ProcessEmbargoLiftedAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Research notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static Guid? ExtractGuid(string payloadJson, string propertyName)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty(propertyName, out var element) && element.TryGetGuid(out var value) ? value : null;
    }

    private async Task ProcessGrantLifecycleAsync(string eventType, CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var grants = scope.ServiceProvider.GetRequiredService<IGrantRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IResearchNotificationPublisher>();

        var messages = await outbox.GetUnprocessedAsync(eventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var grantId = ExtractGuid(message.PayloadJson, "GrantId") ?? throw new InvalidOperationException($"Empty {eventType} payload.");
                var grant = await grants.GetByIdAsync(new GrantId(grantId), cancellationToken).ConfigureAwait(false);
                if (grant is not null)
                {
                    foreach (var investigator in grant.Investigators)
                    {
                        await publisher.PublishAsync(new ResearchNotificationRequest(eventType, grantId.ToString(), investigator.FacultyMemberId, new Dictionary<string, string?> { ["grantId"] = grantId.ToString() }), cancellationToken).ConfigureAwait(false);
                    }
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Research notification relay: {EventType} publish failed for outbox message {MessageId} - will retry next poll.", eventType, message.Id);
            }
        }
    }

    private async Task ProcessPiReassignmentRequiredAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();

        var messages = await outbox.GetUnprocessedAsync(GrantPiReassignmentRequiredEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            logger.LogWarning(
                "Research notification relay: GrantPiReassignmentRequired has no bounded Admin/Research-Office recipient list to resolve (documented gap - see class remarks); skipping fan-out for outbox message {MessageId}.",
                message.Id);
            await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessEmbargoLiftedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var entries = scope.ServiceProvider.GetRequiredService<IInstitutionalRepositoryEntryRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IResearchNotificationPublisher>();

        var messages = await outbox.GetUnprocessedAsync(EmbargoLiftedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var entryId = ExtractGuid(message.PayloadJson, "RepositoryEntryId") ?? throw new InvalidOperationException("Empty InstitutionalRepositoryEntryEmbargoLifted payload.");
                var entry = await entries.GetByIdAsync(new InstitutionalRepositoryEntryId(entryId), cancellationToken).ConfigureAwait(false);
                if (entry is not null)
                {
                    var mergeFields = new Dictionary<string, string?> { ["repositoryEntryId"] = entryId.ToString() };
                    if (entry.Depositor.FacultyMemberId is { } depositorFacultyMemberId)
                    {
                        await publisher.PublishAsync(new ResearchNotificationRequest(EmbargoLiftedEventType, entryId.ToString(), depositorFacultyMemberId, mergeFields), cancellationToken).ConfigureAwait(false);
                    }

                    if (entry.SupervisingFacultyMemberId is { } supervisorId)
                    {
                        await publisher.PublishAsync(new ResearchNotificationRequest(EmbargoLiftedEventType, entryId.ToString(), supervisorId, mergeFields), cancellationToken).ConfigureAwait(false);
                    }
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Research notification relay: InstitutionalRepositoryEntryEmbargoLifted publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
