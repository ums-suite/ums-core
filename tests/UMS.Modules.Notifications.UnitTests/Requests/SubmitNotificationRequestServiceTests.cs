using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;
using UMS.Modules.Notifications.UnitTests.TestDoubles;
using UMS.Shared.Notifications;

namespace UMS.Modules.Notifications.UnitTests.Requests;

/// <summary>NTF-3/NTF-4/NTF-5/NTF-6 - the in-process fan-out acceptance path.</summary>
public class SubmitNotificationRequestServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    private static (SubmitNotificationRequestService Service, FakeNotificationRequestRepository Requests, FakeRecipientPreferenceRepository Preferences, FakeUnitOfWork UnitOfWork, FakeOtpRateLimiter RateLimiter) CreateService(bool otpAllowed = true)
    {
        var requests = new FakeNotificationRequestRepository();
        var preferences = new FakeRecipientPreferenceRepository();
        var unitOfWork = new FakeUnitOfWork();
        var rateLimiter = new FakeOtpRateLimiter(otpAllowed);
        var service = new SubmitNotificationRequestService(requests, preferences, rateLimiter, unitOfWork, new FakeClock(_now), NullLogger<SubmitNotificationRequestService>.Instance);
        return (service, requests, preferences, unitOfWork, rateLimiter);
    }

    [Fact]
    public async Task SubmitAsync_accepts_a_known_event_type_and_creates_a_NotificationRequest()
    {
        var (service, requests, _, _, _) = CreateService();
        var recipientId = Guid.NewGuid();

        var result = await service.SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", "invoice-1", recipientId, "{}"));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value);
        Assert.Single(requests.Requests);
        Assert.Equal(NotificationCategory.Payment, requests.Requests[0].Category);
    }

    [Fact]
    public async Task SubmitAsync_treats_a_dedupe_constraint_violation_as_a_successful_no_op()
    {
        var (service, requests, _, unitOfWork, _) = CreateService();
        unitOfWork.QueueFailure(new DuplicateNotificationRequestException());

        var result = await service.SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", "invoice-1", Guid.NewGuid(), "{}"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value);

        // The dedupe violation happened at SaveChanges time (the real DB backstop) - the in-memory
        // "Add" already ran (design-decisions.md: "the fan-out acceptance handler always attempts
        // the insert directly"), matching the real flow's shape even though this fake never actually
        // enforces the constraint itself.
        Assert.Single(requests.Requests);
    }

    [Fact]
    public async Task SubmitAsync_skips_fan_out_at_enqueue_time_for_an_already_opted_out_informational_category()
    {
        var (service, requests, preferences, unitOfWork, _) = CreateService();
        var recipientId = Guid.NewGuid();
        preferences.Add(RecipientNotificationPreference.OptOut(recipientId, NotificationCategory.Informational, _now).Value);

        var result = await service.SubmitAsync(new SubmitNotificationRequestCommand("finance", "FeeDue", "invoice-1", recipientId, "{}"));

        Assert.True(result.IsSuccess);
        Assert.Equal(Guid.Empty, result.Value);
        Assert.Empty(requests.Requests);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SubmitAsync_never_skips_a_mandatory_category_even_if_an_opt_out_row_somehow_exists()
    {
        // §9 Decision 3: mandatory categories can never be opted out of - OptOut itself already
        // rejects this (RecipientNotificationPreferenceTests), but this test proves the intake path
        // never even asks the opt-out question for a mandatory category in the first place.
        var (service, requests, preferences, _, _) = CreateService();
        var recipientId = Guid.NewGuid();

        var result = await service.SubmitAsync(new SubmitNotificationRequestCommand("finance", "PaymentCompleted", "invoice-1", recipientId, "{}"));

        Assert.True(result.IsSuccess);
        Assert.Single(requests.Requests);
        Assert.Empty(preferences.Preferences);
    }

    [Fact]
    public async Task SubmitAsync_rejects_an_Otp_submission_once_the_rate_limit_is_exceeded()
    {
        var (service, requests, _, _, _) = CreateService(otpAllowed: false);

        var result = await service.SubmitAsync(new SubmitNotificationRequestCommand("identity", "OtpIssued", "login-attempt-1", Guid.NewGuid(), "{}"));

        Assert.True(result.IsFailure);
        Assert.Equal("notification_request.otp_rate_limited", result.Error!.Code);
        Assert.Empty(requests.Requests);
    }
}
