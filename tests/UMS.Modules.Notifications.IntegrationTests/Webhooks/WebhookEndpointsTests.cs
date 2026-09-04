using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Dispatch;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Application.Templates;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.IntegrationTests.Infrastructure;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.IntegrationTests.Webhooks;

/// <summary>NTF-15: provider bounce/delivery-receipt callbacks. §8 edge case: "Hard email bounce -&gt; the recipient's email is marked suppressed for future automated sends".</summary>
[Collection(NotificationsApiTestCollectionDefinition.Name)]
public class WebhookEndpointsTests(NotificationsApiFixture fixture)
{
    [Fact]
    public async Task A_hard_bounce_callback_suppresses_the_recipients_email_channel_for_future_sends()
    {
        using var client = fixture.CreateClient();
        var recipient = await TestUsers.ProvisionAsync(client);

        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var requests = scope.ServiceProvider.GetRequiredService<INotificationRequestRepository>();
        var dispatch = scope.ServiceProvider.GetRequiredService<NotificationDispatchService>();
        var templates = scope.ServiceProvider.GetRequiredService<TemplateManagementService>();
        var suppressions = scope.ServiceProvider.GetRequiredService<IChannelSuppressionRepository>();

        await templates.UpdateAsync(
            (await templates.GetOrCreateAsync("PaymentCompleted", NotificationChannel.Email)).Value.Id,
            new UpsertTemplateTranslationCommand("en", "Payment received", "Thanks for your payment.", null, null));

        var submitted = await intake.SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", $"invoice-{Guid.NewGuid():N}", recipient.Id, "{}"));
        var request = await requests.GetByIdAsync(new NotificationRequestId(submitted.Value));
        var emailAttempt = request!.Attempts.Single(a => a.Channel == NotificationChannel.Email);

        // Dispatch once so the attempt has a real ProviderMessageId to correlate the webhook against.
        await dispatch.ProcessClaimedAsync(new ClaimedAttempt(emailAttempt.Id.Value, request.Id.Value, 0));
        var attempts = scope.ServiceProvider.GetRequiredService<INotificationDeliveryAttemptRepository>();
        var delivered = await attempts.GetByIdAsync(emailAttempt.Id);
        Assert.Equal(DeliveryAttemptStatus.Delivered, delivered!.Status);
        Assert.NotNull(delivered.ProviderMessageId);

        var webhookBody = new { providerMessageId = delivered.ProviderMessageId, @event = 2 /* HardBounce */, reason = "mailbox_does_not_exist" };
        var webhookResponse = await client.PostAsJsonAsync("/api/v1/notifications/webhooks/email", webhookBody);
        webhookResponse.EnsureSuccessStatusCode();

        Assert.True(await suppressions.IsSuppressedAsync(recipient.Id, NotificationChannel.Email));
    }
}
