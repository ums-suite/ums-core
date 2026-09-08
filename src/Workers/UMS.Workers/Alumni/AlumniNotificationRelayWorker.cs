using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Modules.Alumni.Domain.Events;
using UMS.Shared.Student;

namespace UMS.Workers.Alumni;

/// <summary>
/// ALM-15: fans Alumni's own domain events out to Notifications (requirement-spec.md §3 Consumers
/// columns, §7) - mirrors Content's own <c>NoticeNotificationRelayWorker</c>/Research's own
/// <c>ResearchNotificationRelayWorker</c> shape.
///
/// <para>
/// Every recipient here is resolved indirectly through <c>UMS.Shared.Student.IStudentStatusChecker</c>
/// (the same sanctioned path <see cref="Alumnus"/>'s own remarks describe) - an <see cref="Alumnus"/>
/// has no Identity <c>User</c> id of its own, only a <c>StudentIdRef</c>, so the recipient is always
/// "the Identity User backing the originating Student record".
/// </para>
///
/// <para>
/// <b>Known, documented gap</b> - <c>JobPostingPublished</c>'s "digest to opted-in alumni" (§3) has no
/// bounded recipient list to resolve: no per-alumnus job-digest opt-in flag/list is built in this v1
/// (not named by any of the 16 tickets). Logged and skipped rather than guessed at, the same posture
/// Research's own <c>ResearchNotificationRelayWorker</c> already documents for
/// <c>GrantPiReassignmentRequired</c>'s unresolvable Admin/Research-Office broadcast.
/// </para>
/// </summary>
public sealed class AlumniNotificationRelayWorker(IServiceScopeFactory scopeFactory, ILogger<AlumniNotificationRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private static readonly string AlumnusCreatedEventType = typeof(AlumnusCreated).Name;
    private static readonly string JobPostingPublishedEventType = typeof(JobPostingPublished).Name;
    private static readonly string DonationConfirmedEventType = typeof(DonationConfirmed).Name;
    private static readonly string MentorshipMatchAcceptedEventType = typeof(MentorshipMatchAccepted).Name;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAlumnusCreatedAsync(stoppingToken).ConfigureAwait(false);
                await ProcessJobPostingPublishedAsync(stoppingToken).ConfigureAwait(false);
                await ProcessDonationConfirmedAsync(stoppingToken).ConfigureAwait(false);
                await ProcessMentorshipMatchAcceptedAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alumni notification relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private static Guid? ExtractGuid(string payloadJson, string propertyName)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty(propertyName, out var element) && element.TryGetGuid(out var value) ? value : null;
    }

    /// <summary>Welcome email (requirement-spec.md §3 AlumnusCreated).</summary>
    private async Task ProcessAlumnusCreatedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var studentStatusChecker = scope.ServiceProvider.GetRequiredService<IStudentStatusChecker>();
        var publisher = scope.ServiceProvider.GetRequiredService<IAlumniNotificationPublisher>();

        var messages = await outbox.GetUnprocessedAsync(AlumnusCreatedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var alumnusId = ExtractGuid(message.PayloadJson, "AlumnusId") ?? throw new InvalidOperationException($"Empty {AlumnusCreatedEventType} payload.");
                var studentIdRef = ExtractGuid(message.PayloadJson, "StudentIdRef") ?? throw new InvalidOperationException($"Empty {AlumnusCreatedEventType} payload.");

                var standing = await studentStatusChecker.GetByStudentIdAsync(studentIdRef, cancellationToken).ConfigureAwait(false);
                if (standing?.IdentityUserId is { } recipientId)
                {
                    await publisher.PublishAsync(new AlumniNotificationRequest(AlumnusCreatedEventType, alumnusId.ToString(), recipientId, new Dictionary<string, string?> { ["alumnusId"] = alumnusId.ToString() }), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning("Alumni notification relay: AlumnusCreated for Alumnus {AlumnusId} has no resolvable Identity recipient - skipping welcome email.", alumnusId);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Alumni notification relay: AlumnusCreated publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }

    /// <summary>Documented gap - see class remarks.</summary>
    private async Task ProcessJobPostingPublishedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();

        var messages = await outbox.GetUnprocessedAsync(JobPostingPublishedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            logger.LogWarning(
                "Alumni notification relay: JobPostingPublished's 'digest to opted-in alumni' has no bounded recipient list to resolve in this v1 (documented gap - see class remarks); skipping fan-out for outbox message {MessageId}.",
                message.Id);
            await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Donation receipt (requirement-spec.md §3 DonationConfirmed).</summary>
    private async Task ProcessDonationConfirmedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var alumni = scope.ServiceProvider.GetRequiredService<IAlumnusRepository>();
        var studentStatusChecker = scope.ServiceProvider.GetRequiredService<IStudentStatusChecker>();
        var publisher = scope.ServiceProvider.GetRequiredService<IAlumniNotificationPublisher>();

        var messages = await outbox.GetUnprocessedAsync(DonationConfirmedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var donationId = ExtractGuid(message.PayloadJson, "DonationId") ?? throw new InvalidOperationException($"Empty {DonationConfirmedEventType} payload.");
                var alumnusId = ExtractGuid(message.PayloadJson, "AlumnusId") ?? throw new InvalidOperationException($"Empty {DonationConfirmedEventType} payload.");

                var alumnus = await alumni.GetByIdAsync(new AlumnusId(alumnusId), cancellationToken).ConfigureAwait(false);
                var standing = alumnus is null ? null : await studentStatusChecker.GetByStudentIdAsync(alumnus.StudentIdRef, cancellationToken).ConfigureAwait(false);
                if (standing?.IdentityUserId is { } recipientId)
                {
                    await publisher.PublishAsync(new AlumniNotificationRequest(DonationConfirmedEventType, donationId.ToString(), recipientId, new Dictionary<string, string?> { ["donationId"] = donationId.ToString() }), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    logger.LogWarning("Alumni notification relay: DonationConfirmed for Donation {DonationId} has no resolvable Identity recipient - skipping receipt.", donationId);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Alumni notification relay: DonationConfirmed publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }

    /// <summary>Mentorship match notices to both mentor and mentee (requirement-spec.md §3 MentorshipMatchAccepted).</summary>
    private async Task ProcessMentorshipMatchAcceptedAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();
        var alumni = scope.ServiceProvider.GetRequiredService<IAlumnusRepository>();
        var studentStatusChecker = scope.ServiceProvider.GetRequiredService<IStudentStatusChecker>();
        var publisher = scope.ServiceProvider.GetRequiredService<IAlumniNotificationPublisher>();

        var messages = await outbox.GetUnprocessedAsync(MentorshipMatchAcceptedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            try
            {
                var matchId = ExtractGuid(message.PayloadJson, "MentorshipMatchId") ?? throw new InvalidOperationException($"Empty {MentorshipMatchAcceptedEventType} payload.");
                var mentorAlumnusId = ExtractGuid(message.PayloadJson, "MentorAlumnusId") ?? throw new InvalidOperationException($"Empty {MentorshipMatchAcceptedEventType} payload.");
                var menteeStudentId = ExtractGuid(message.PayloadJson, "MenteeStudentId") ?? throw new InvalidOperationException($"Empty {MentorshipMatchAcceptedEventType} payload.");

                var mergeFields = new Dictionary<string, string?> { ["mentorshipMatchId"] = matchId.ToString() };

                var mentor = await alumni.GetByIdAsync(new AlumnusId(mentorAlumnusId), cancellationToken).ConfigureAwait(false);
                var mentorStanding = mentor is null ? null : await studentStatusChecker.GetByStudentIdAsync(mentor.StudentIdRef, cancellationToken).ConfigureAwait(false);
                if (mentorStanding?.IdentityUserId is { } mentorRecipientId)
                {
                    await publisher.PublishAsync(new AlumniNotificationRequest(MentorshipMatchAcceptedEventType, matchId.ToString(), mentorRecipientId, mergeFields), cancellationToken).ConfigureAwait(false);
                }

                var menteeStanding = await studentStatusChecker.GetByStudentIdAsync(menteeStudentId, cancellationToken).ConfigureAwait(false);
                if (menteeStanding?.IdentityUserId is { } menteeRecipientId)
                {
                    await publisher.PublishAsync(new AlumniNotificationRequest(MentorshipMatchAcceptedEventType, matchId.ToString(), menteeRecipientId, mergeFields), cancellationToken).ConfigureAwait(false);
                }

                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(ex, "Alumni notification relay: MentorshipMatchAccepted publish failed for outbox message {MessageId} - will retry next poll.", message.Id);
            }
        }
    }
}
