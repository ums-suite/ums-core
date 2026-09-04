using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Preferences;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.IntegrationTests.Infrastructure;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.IntegrationTests.Requests;

/// <summary>
/// NTF-3/NTF-4/NTF-5/NTF-6 against a real Postgres instance - exercised by calling
/// <see cref="INotificationRequestIntake"/> directly, in-process, exactly as a future publishing
/// module's own outbox relay would (requirement-spec.md §9 Decision 1: there is no public inbound
/// HTTP endpoint for this - see that interface's own remarks).
/// </summary>
[Collection(NotificationsApiTestCollectionDefinition.Name)]
public class FanOutAndDedupTests(NotificationsApiFixture fixture)
{
    [Fact]
    public async Task SubmitAsync_fans_out_into_one_delivery_attempt_per_configured_channel()
    {
        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var requests = scope.ServiceProvider.GetRequiredService<INotificationRequestRepository>();

        var recipientId = Guid.NewGuid();
        var result = await intake.SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", $"invoice-{Guid.NewGuid():N}", recipientId, "{}"));

        Assert.True(result.IsSuccess);
        var request = await requests.GetByIdAsync(new NotificationRequestId(result.Value));
        Assert.NotNull(request);
        Assert.Equal(3, request!.Attempts.Count); // PaymentCompleted -> Email, Sms, InApp per the event catalog.
    }

    [Fact]
    public async Task SubmitAsync_deduplicates_a_repeated_submission_for_the_same_natural_key_via_the_real_DB_constraint()
    {
        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var requests = scope.ServiceProvider.GetRequiredService<INotificationRequestRepository>();

        var recipientId = Guid.NewGuid();
        var sourceEntityId = $"invoice-{Guid.NewGuid():N}";
        var command = new SubmitNotificationRequestCommand("finance", "PaymentCompleted", sourceEntityId, recipientId, "{}");

        // Simulates the at-least-once outbox redelivery edge case (edge-cases.md, "Two modules ...
        // raising the same logical NotificationRequest concurrently") - two submissions for the
        // exact same natural key.
        var first = await intake.SubmitAsync(command);
        var second = await intake.SubmitAsync(command);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(Guid.Empty, second.Value); // "already accepted" - a successful no-op, not an error.

        var stored = await requests.GetByIdsAsync([first.Value]);
        Assert.Single(stored);
    }

    [Fact]
    public async Task SubmitAsync_skips_fan_out_for_a_recipient_who_opted_out_of_the_Informational_category()
    {
        using var scope = fixture.Services.CreateScope();
        var intake = scope.ServiceProvider.GetRequiredService<INotificationRequestIntake>();
        var preferenceService = scope.ServiceProvider.GetRequiredService<RecipientPreferenceService>();
        var requests = scope.ServiceProvider.GetRequiredService<INotificationRequestRepository>();

        var recipientId = Guid.NewGuid();
        var optOutResult = await preferenceService.SetOptOutAsync(recipientId, NotificationCategory.Informational, optedOut: true);
        Assert.True(optOutResult.IsSuccess);

        var result = await intake.SubmitAsync(new SubmitNotificationRequestCommand("finance", "FeeDue", $"invoice-{Guid.NewGuid():N}", recipientId, "{}"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value);
        var stored = await requests.GetByIdsAsync([result.Value]);
        Assert.DoesNotContain(stored, r => r.RecipientId == recipientId);
    }

    [Fact]
    public async Task SetOptOutAsync_rejects_opting_out_of_a_mandatory_category()
    {
        using var scope = fixture.Services.CreateScope();
        var preferenceService = scope.ServiceProvider.GetRequiredService<RecipientPreferenceService>();

        var result = await preferenceService.SetOptOutAsync(Guid.NewGuid(), NotificationCategory.Payment, optedOut: true);

        Assert.True(result.IsFailure);
        Assert.Equal("notification_preference.mandatory_category", result.Error!.Code);
    }
}
