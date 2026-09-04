using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Dispatch;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Application.Templates;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.IntegrationTests.Infrastructure;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.IntegrationTests.Dispatch;

/// <summary>
/// NTF-13's real row-lock-guarded claim query and a full send round trip through the real
/// <c>HttpClientFactory</c> + Polly + fake-gateway pipeline (see each channel provider's own remarks
/// on why the gateway itself is fake, not real Twilio/SendGrid).
///
/// <para>
/// Every test here deliberately uses a FRESH DI scope (hence a fresh <c>NotificationsDbContext</c>)
/// per logical step (submit / claim / process), mirroring exactly how the real
/// <c>NotificationChannelDispatchWorkerBase</c> and a real publishing module's own outbox relay are
/// always separate scopes in production - reusing one scope across submit+claim+read would hit EF
/// Core's own identity-map staleness (a since-raw-SQL-updated row's already-tracked in-memory
/// instance is not automatically refreshed), which is a test-authoring pitfall, not a product bug.
/// </para>
/// </summary>
[Collection(NotificationsApiTestCollectionDefinition.Name)]
public class ChannelClaimAndDispatchTests(NotificationsApiFixture fixture)
{
    [Fact]
    public async Task ClaimBatchAsync_claims_a_pending_attempt_and_transitions_it_to_InFlight()
    {
        var requestId = await SubmitAsync("finance", "PaymentCompleted", Guid.NewGuid());

        using (var claimScope = fixture.Services.CreateScope())
        {
            var claimed = await claimScope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>()
                .ClaimBatchAsync(NotificationChannel.Email, [NotificationPriority.Expedited, NotificationPriority.Standard], 100, DateTimeOffset.UtcNow);
            Assert.Contains(claimed, c => c.NotificationRequestId == requestId);
        }

        using var readScope = fixture.Services.CreateScope();
        var request = await readScope.ServiceProvider.GetRequiredService<INotificationRequestRepository>().GetByIdAsync(new NotificationRequestId(requestId));
        var emailAttempt = request!.Attempts.Single(a => a.Channel == NotificationChannel.Email);
        Assert.Equal(DeliveryAttemptStatus.InFlight, emailAttempt.Status);
    }

    [Fact]
    public async Task ClaimBatchAsync_does_not_reclaim_an_attempt_still_within_its_InFlight_timeout()
    {
        var requestId = await SubmitAsync("finance", "PaymentCompleted", Guid.NewGuid());
        var now = DateTimeOffset.UtcNow;

        using var scope = fixture.Services.CreateScope();
        var attempts = scope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>();

        var firstClaim = await attempts.ClaimBatchAsync(NotificationChannel.Email, [NotificationPriority.Expedited, NotificationPriority.Standard], 100, now);
        Assert.Contains(firstClaim, c => c.NotificationRequestId == requestId);

        // Same "now" - the just-claimed row is InFlight and its timeout has not elapsed, so a second
        // claim pass must not pick it up again (design-decisions.md "Retry/Backoff Design Per
        // Channel" - no duplicate concurrent dispatch of the same attempt).
        var secondClaim = await attempts.ClaimBatchAsync(NotificationChannel.Email, [NotificationPriority.Expedited, NotificationPriority.Standard], 100, now);
        Assert.DoesNotContain(secondClaim, c => c.NotificationRequestId == requestId);
    }

    [Fact]
    public async Task A_claimed_attempt_is_delivered_through_the_real_HttpClientFactory_Polly_fake_gateway_pipeline()
    {
        using var client = fixture.CreateClient();
        var recipient = await TestUsers.ProvisionAsync(client);

        await SeedTemplateAsync("PaymentCompleted", NotificationChannel.Email, subject: "Payment received", body: "Thanks {{name}}!");
        var requestId = await SubmitAsync("finance", "PaymentCompleted", recipient.Id, """{"name":"Rafi"}""");

        var claimed = await ClaimSingleAsync(NotificationChannel.Email, requestId);

        using (var processScope = fixture.Services.CreateScope())
        {
            await processScope.ServiceProvider.GetRequiredService<NotificationDispatchService>().ProcessClaimedAsync(claimed);
        }

        using var readScope = fixture.Services.CreateScope();
        var delivered = await readScope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>().GetByIdAsync(new NotificationDeliveryAttemptId(claimed.AttemptId));
        Assert.Equal(DeliveryAttemptStatus.Delivered, delivered!.Status);
        Assert.StartsWith("fake-email-", delivered.ProviderMessageId);
        Assert.Equal("Thanks Rafi!", delivered.RenderedBody);
    }

    /// <summary>edge-cases.md: "SMS provider outage -&gt; the SMS queue backs up and dead-letters after bounded retries ... no other module's request path is affected."</summary>
    [Fact]
    public async Task A_forced_provider_outage_dead_letters_the_attempt_after_the_bounded_retry_budget_is_exhausted()
    {
        await using var outageFactory = fixture.WithWebHostBuilder(builder =>
            builder.UseSetting("Notifications:FakeGateway:Sms:ForceOutage", "true"));

        using var client = outageFactory.CreateClient();
        var recipient = await TestUsers.ProvisionAsync(client, mobile: "01712345678");

        using (var templateScope = outageFactory.Services.CreateScope())
        {
            var templates = templateScope.ServiceProvider.GetRequiredService<TemplateManagementService>();
            await templates.UpdateAsync(
                (await templates.GetOrCreateAsync("PaymentCompleted", NotificationChannel.Sms)).Value.Id,
                new UpsertTemplateTranslationCommand("en", null, "Payment received.", null, null));
        }

        Guid requestId;
        using (var submitScope = outageFactory.Services.CreateScope())
        {
            var submitted = await submitScope.ServiceProvider.GetRequiredService<INotificationRequestIntake>()
                .SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", $"invoice-{Guid.NewGuid():N}", recipient.Id, "{}"));
            Assert.True(submitted.IsSuccess);
            requestId = submitted.Value;
        }

        ClaimedAttempt claimed;
        using (var claimScope = outageFactory.Services.CreateScope())
        {
            claimed = (await claimScope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>()
                    .ClaimBatchAsync(NotificationChannel.Sms, [NotificationPriority.Expedited, NotificationPriority.Standard], 100, DateTimeOffset.UtcNow))
                .Single(c => c.NotificationRequestId == requestId);
        }

        // Drives the same claimed attempt through the dispatch pipeline repeatedly, exactly as
        // successive worker poll ticks would once each retry's backoff elapses (NTF-13) - the
        // outage forces every one of the fake gateway's own responses to fail, so this exhausts the
        // bounded retry budget deterministically without a test actually waiting out real backoff
        // delays. Each pass uses its own fresh scope, exactly as the real worker's own poll loop
        // would (see this class's own remarks on scope reuse).
        for (var i = 0; i < NotificationDeliveryAttempt.MaxAttempts; i++)
        {
            using var processScope = outageFactory.Services.CreateScope();
            await processScope.ServiceProvider.GetRequiredService<NotificationDispatchService>().ProcessClaimedAsync(claimed);
        }

        using var readScope = outageFactory.Services.CreateScope();
        var final = await readScope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>().GetByIdAsync(new NotificationDeliveryAttemptId(claimed.AttemptId));
        Assert.Equal(DeliveryAttemptStatus.DeadLettered, final!.Status);
        Assert.Equal(DeadLetterReason.RetriesExhausted, final.DeadLetterReason);
        Assert.Equal(NotificationDeliveryAttempt.MaxAttempts, final.AttemptCount);
    }

    private async Task<Guid> SubmitAsync(string sourceModule, string eventType, Guid recipientId, string payloadJson = "{}")
    {
        using var scope = fixture.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>()
            .SubmitAsync(new SubmitNotificationRequestCommand(sourceModule, eventType, $"entity-{Guid.NewGuid():N}", recipientId, payloadJson));
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task SeedTemplateAsync(string eventType, NotificationChannel channel, string? subject, string body)
    {
        using var scope = fixture.Services.CreateScope();
        var templates = scope.ServiceProvider.GetRequiredService<TemplateManagementService>();
        var template = await templates.GetOrCreateAsync(eventType, channel);
        await templates.UpdateAsync(template.Value.Id, new UpsertTemplateTranslationCommand("en", subject, body, null, null));
    }

    private async Task<ClaimedAttempt> ClaimSingleAsync(NotificationChannel channel, Guid requestId)
    {
        using var scope = fixture.Services.CreateScope();
        var claimed = await scope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>()
            .ClaimBatchAsync(channel, [NotificationPriority.Expedited, NotificationPriority.Standard], 100, DateTimeOffset.UtcNow);
        return claimed.Single(c => c.NotificationRequestId == requestId);
    }
}
