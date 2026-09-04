using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.UnitTests.Requests;

public class NotificationRequestTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_with_valid_input_creates_one_pending_attempt_per_requested_channel()
    {
        var result = NotificationRequest.Create(
            "finance", "PaymentCompleted", "invoice-1", Guid.NewGuid(),
            NotificationCategory.Payment, NotificationPriority.Standard,
            [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.InApp],
            "{}", null, _now);

        Assert.True(result.IsSuccess);
        var request = result.Value;
        Assert.Equal(3, request.Attempts.Count);
        Assert.All(request.Attempts, a => Assert.Equal(DeliveryAttemptStatus.Pending, a.Status));
        Assert.Contains(request.Attempts, a => a.Channel == NotificationChannel.Email);
        Assert.Contains(request.Attempts, a => a.Channel == NotificationChannel.Sms);
        Assert.Contains(request.Attempts, a => a.Channel == NotificationChannel.InApp);
    }

    [Fact]
    public void Create_deduplicates_repeated_channels_in_the_requested_set()
    {
        var result = NotificationRequest.Create(
            "finance", "PaymentCompleted", "invoice-1", Guid.NewGuid(),
            NotificationCategory.Payment, NotificationPriority.Standard,
            [NotificationChannel.Email, NotificationChannel.Email],
            "{}", null, _now);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Attempts);
    }

    [Fact]
    public void Create_raises_NotificationRequestReceived()
    {
        var result = NotificationRequest.Create(
            "finance", "PaymentCompleted", "invoice-1", Guid.NewGuid(),
            NotificationCategory.Payment, NotificationPriority.Standard,
            [NotificationChannel.Email],
            "{}", null, _now);

        var domainEvent = Assert.Single(result.Value.DomainEvents);
        Assert.IsType<UMS.Modules.Notifications.Domain.Events.NotificationRequestReceived>(domainEvent);
    }

    [Theory]
    [InlineData("", "EventType", "entity-1")]
    [InlineData("module", "", "entity-1")]
    [InlineData("module", "EventType", "")]
    public void Create_rejects_missing_required_identifiers(string sourceModule, string eventType, string sourceEntityId)
    {
        var result = NotificationRequest.Create(
            sourceModule, eventType, sourceEntityId, Guid.NewGuid(),
            NotificationCategory.Transactional, NotificationPriority.Standard,
            [NotificationChannel.Email],
            "{}", null, _now);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Create_rejects_an_empty_channel_set()
    {
        var result = NotificationRequest.Create(
            "module", "EventType", "entity-1", Guid.NewGuid(),
            NotificationCategory.Transactional, NotificationPriority.Standard,
            [],
            "{}", null, _now);

        Assert.True(result.IsFailure);
        Assert.Equal("notification_request.no_channels", result.Error!.Code);
    }
}
