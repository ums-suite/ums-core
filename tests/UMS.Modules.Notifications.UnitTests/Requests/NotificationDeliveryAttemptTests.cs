using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;

namespace UMS.Modules.Notifications.UnitTests.Requests;

/// <summary>
/// Per-channel independent retry/dead-letter state machine (design-decisions.md "Retry/Backoff
/// Design Per Channel"; §4 invariant "Per-channel retry/dead-letter state is independent"; §4
/// invariant "All delivery attempts are idempotent by construction").
/// </summary>
public class NotificationDeliveryAttemptTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private static NotificationDeliveryAttempt CreateAttempt(NotificationChannel channel = NotificationChannel.Email)
    {
        var result = NotificationRequest.Create(
            "finance", "PaymentCompleted", "invoice-1", Guid.NewGuid(),
            NotificationCategory.Payment, NotificationPriority.Standard,
            [channel], "{}", null, _now);

        return result.Value.Attempts.Single();
    }

    [Fact]
    public void New_attempt_is_pending_and_eligible_for_dispatch()
    {
        var attempt = CreateAttempt();

        Assert.Equal(DeliveryAttemptStatus.Pending, attempt.Status);
        Assert.True(attempt.IsEligibleForDispatch(_now));
    }

    [Fact]
    public void MarkDelivered_transitions_to_Delivered_and_is_no_longer_eligible()
    {
        var attempt = CreateAttempt();

        attempt.MarkDelivered(_now, "provider-msg-1", "subject", "body", null);

        Assert.Equal(DeliveryAttemptStatus.Delivered, attempt.Status);
        Assert.Equal("provider-msg-1", attempt.ProviderMessageId);
        Assert.False(attempt.IsEligibleForDispatch(_now));
    }

    [Fact]
    public void MarkDelivered_is_idempotent_a_second_call_is_a_no_op()
    {
        var attempt = CreateAttempt();
        attempt.MarkDelivered(_now, "first-message-id", "s", "b", null);

        attempt.MarkFailedTransient("a retry racing the original attempt", _now.AddSeconds(1));

        Assert.Equal(DeliveryAttemptStatus.Delivered, attempt.Status);
        Assert.Equal("first-message-id", attempt.ProviderMessageId);
        Assert.Equal(0, attempt.AttemptCount);
    }

    [Fact]
    public void MarkFailedTransient_schedules_a_bounded_exponential_backoff_retry_before_exhaustion()
    {
        var attempt = CreateAttempt();

        var deadLettered = attempt.MarkFailedTransient("simulated gateway timeout", _now);

        Assert.False(deadLettered);
        Assert.Equal(DeliveryAttemptStatus.Retrying, attempt.Status);
        Assert.Equal(1, attempt.AttemptCount);
        Assert.NotNull(attempt.NextAttemptAt);
        Assert.True(attempt.NextAttemptAt > _now);
        Assert.False(attempt.IsEligibleForDispatch(_now));
        Assert.True(attempt.IsEligibleForDispatch(attempt.NextAttemptAt!.Value));
    }

    [Fact]
    public void MarkFailedTransient_dead_letters_with_RetriesExhausted_after_the_bounded_retry_budget()
    {
        var attempt = CreateAttempt();
        var now = _now;

        bool deadLettered = false;
        for (var i = 0; i < NotificationDeliveryAttempt.MaxAttempts; i++)
        {
            deadLettered = attempt.MarkFailedTransient($"failure #{i}", now);
            now = now.AddMinutes(5);
        }

        Assert.True(deadLettered);
        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Equal(DeadLetterReason.RetriesExhausted, attempt.DeadLetterReason);
        Assert.Equal(NotificationDeliveryAttempt.MaxAttempts, attempt.AttemptCount);
        Assert.False(attempt.IsEligibleForDispatch(now));
    }

    [Fact]
    public void MarkNoContactInfo_dead_letters_immediately_with_zero_attempts_spent()
    {
        var attempt = CreateAttempt();

        attempt.MarkNoContactInfo(_now);

        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Equal(DeadLetterReason.NoContactInfo, attempt.DeadLetterReason);
        Assert.Equal(0, attempt.AttemptCount);
    }

    [Fact]
    public void MarkTemplateMissing_dead_letters_immediately_without_retrying()
    {
        var attempt = CreateAttempt();

        attempt.MarkTemplateMissing(_now);

        Assert.Equal(DeliveryAttemptStatus.DeadLettered, attempt.Status);
        Assert.Equal(DeadLetterReason.TemplateMissing, attempt.DeadLetterReason);
        Assert.Equal(0, attempt.AttemptCount);
    }

    [Fact]
    public void MarkSuppressedByOptOut_is_a_successful_terminal_state_not_a_dead_letter()
    {
        var attempt = CreateAttempt();

        attempt.MarkSuppressedByOptOut(_now);

        Assert.Equal(DeliveryAttemptStatus.Suppressed, attempt.Status);
        Assert.Null(attempt.DeadLetterReason);
    }

    [Fact]
    public void An_InFlight_attempt_is_not_eligible_for_retry_until_the_timeout_elapses()
    {
        var attempt = CreateAttempt();
        attempt.MarkInFlight(_now);

        Assert.False(attempt.IsEligibleForDispatch(_now + NotificationDeliveryAttempt.InFlightTimeout - TimeSpan.FromSeconds(1)));
        Assert.True(attempt.IsEligibleForDispatch(_now + NotificationDeliveryAttempt.InFlightTimeout + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void MarkRead_is_idempotent_and_only_meaningful_once()
    {
        var attempt = CreateAttempt(NotificationChannel.InApp);
        attempt.MarkDelivered(_now, null, null, "body", null);

        attempt.MarkRead(_now.AddMinutes(1));
        var firstReadAt = attempt.ReadAt;
        attempt.MarkRead(_now.AddMinutes(5));

        Assert.Equal(firstReadAt, attempt.ReadAt);
    }
}
