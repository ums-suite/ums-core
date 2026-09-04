using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Dispatch;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.Domain.Templates;
using UMS.Modules.Notifications.UnitTests.TestDoubles;
using UMS.Shared.Domain;
using UMS.Shared.Identity;

namespace UMS.Modules.Notifications.UnitTests.Dispatch;

/// <summary>
/// NTF-6/9/10/11/12/13/17's per-attempt dispatch pipeline - every branch named in edge-cases.md/§8
/// exercised directly, without a real database or HTTP call.
/// </summary>
public class NotificationDispatchServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private sealed class Fixture
    {
        public FakeNotificationRequestRepository Requests { get; } = new();

        public FakeNotificationDeliveryAttemptRepository Attempts { get; } = new();

        public FakeTemplateRepository Templates { get; } = new();

        public FakeRecipientPreferenceRepository Preferences { get; } = new();

        public FakeChannelSuppressionRepository Suppressions { get; } = new();

        public FakeAuditRecorder Audit { get; } = new();

        public FakeDomainEventRecorder DomainEvents { get; } = new();

        public FakeUnitOfWork UnitOfWork { get; } = new();

        public RecipientContactInfo? ContactInfo { get; set; } = new(Email.Create("student@example.com").Value, PhoneNumber.Create("+8801712345678").Value, null, "en");

        public Func<ChannelSendRequest, ChannelSendResult>? ProviderResponse { get; set; }

        public NotificationDispatchService Build()
        {
            var provider = new FakeChannelProvider(NotificationChannel.Email, ProviderResponse ?? (_ => ChannelSendResult.Success("provider-msg-1")));
            var providers = new Dictionary<NotificationChannel, IChannelProvider> { [NotificationChannel.Email] = provider };

            return new NotificationDispatchService(
                Requests,
                Attempts,
                Templates,
                Preferences,
                Suppressions,
                new FakeRecipientDirectory(ContactInfo),
                providers,
                Audit,
                DomainEvents,
                UnitOfWork,
                new FakeClock(_now),
                NullLogger<NotificationDispatchService>.Instance);
        }

        public (NotificationRequest Request, NotificationDeliveryAttempt Attempt) SeedRequest(NotificationCategory category, NotificationChannel channel = NotificationChannel.Email, string eventType = "PaymentCompleted")
        {
            var request = NotificationRequest.Create("finance", eventType, "invoice-1", Guid.NewGuid(), category, NotificationPriority.Standard, [channel], "{}", null, _now).Value;
            Requests.Add(request);
            var attempt = request.Attempts.Single();
            Attempts.AddRange([attempt]);
            return (request, attempt);
        }

        public void SeedTemplate(string eventType, NotificationChannel channel, string subject = "Subject", string body = "Body")
        {
            var template = Template.Create(eventType, channel, _now).Value;
            template.UpsertTranslation("en", channel == NotificationChannel.InApp ? null : subject, body, channel == NotificationChannel.Push ? "Title" : null, null, _now);
            Templates.Add(template);
        }
    }

    [Fact]
    public async Task Opted_out_informational_category_is_suppressed_at_send_time()
    {
        var fixture = new Fixture();
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Informational);
        fixture.Preferences.Add(RecipientNotificationPreference.OptOut(request.RecipientId, NotificationCategory.Informational, _now).Value);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.Suppressed, attempt.Status);
    }

    [Fact]
    public async Task Suppressed_channel_from_a_prior_hard_bounce_dead_letters_without_calling_the_provider()
    {
        var fixture = new Fixture();
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional);
        fixture.Suppressions.Add(ChannelSuppression.Create(request.RecipientId, NotificationChannel.Email, "hard bounce", _now));
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Equal(DeadLetterReason.ProviderRejected, attempt.DeadLetterReason);
    }

    [Fact]
    public async Task Missing_contact_info_dead_letters_with_NoContactInfo_instead_of_retrying()
    {
        var fixture = new Fixture { ContactInfo = new RecipientContactInfo(null, null, null, "en") };
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Equal(DeadLetterReason.NoContactInfo, attempt.DeadLetterReason);
        Assert.Equal(0, attempt.AttemptCount);
    }

    [Fact]
    public async Task Missing_template_in_every_language_including_English_dead_letters_with_TemplateMissing()
    {
        var fixture = new Fixture();
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional);
        // No template seeded at all.

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Equal(DeadLetterReason.TemplateMissing, attempt.DeadLetterReason);
    }

    [Fact]
    public async Task InApp_channel_always_succeeds_once_it_reaches_dispatch_with_no_external_provider_call()
    {
        var fixture = new Fixture();
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional, NotificationChannel.InApp);
        fixture.SeedTemplate(request.EventType, NotificationChannel.InApp, body: "You have a new notice.");

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.Delivered, attempt.Status);
        Assert.Equal("You have a new notice.", attempt.RenderedBody);
    }

    [Fact]
    public async Task Successful_provider_send_marks_the_attempt_Delivered()
    {
        var fixture = new Fixture();
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.Delivered, attempt.Status);
        Assert.Equal("provider-msg-1", attempt.ProviderMessageId);
    }

    [Fact]
    public async Task A_transient_provider_failure_schedules_a_retry_rather_than_dead_lettering_immediately()
    {
        var fixture = new Fixture { ProviderResponse = _ => ChannelSendResult.TransientFailure("simulated gateway timeout") };
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.Retrying, attempt.Status);
        Assert.Equal(1, attempt.AttemptCount);
    }

    [Fact]
    public async Task A_permanent_provider_rejection_dead_letters_immediately()
    {
        var fixture = new Fixture { ProviderResponse = _ => ChannelSendResult.PermanentFailure("invalid destination") };
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Equal(DeadLetterReason.ProviderRejected, attempt.DeadLetterReason);
    }

    [Fact]
    public async Task A_delivered_Payment_category_attempt_is_audited_NTF17()
    {
        var fixture = new Fixture();
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Payment);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.Delivered, attempt.Status);
        Assert.Single(fixture.Audit.RecordedEntries);
    }

    [Fact]
    public async Task A_delivered_Transactional_category_attempt_is_not_audited()
    {
        var fixture = new Fixture();
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Transactional);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.Delivered, attempt.Status);
        Assert.Empty(fixture.Audit.RecordedEntries);
    }

    [Fact]
    public async Task A_dead_lettered_Result_category_attempt_is_audited_NTF17()
    {
        var fixture = new Fixture { ContactInfo = null };
        var (request, attempt) = fixture.SeedRequest(NotificationCategory.Result);
        fixture.SeedTemplate(request.EventType, NotificationChannel.Email);

        await fixture.Build().ProcessClaimedAsync(new ClaimedAttempt(attempt.Id.Value, request.Id.Value, 0));

        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Single(fixture.Audit.RecordedEntries);
    }
}
