using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.UnitTests.Preferences;

/// <summary>§9 Decision 3: "category-level opt-out ... mandatory transactional vs. optional informational categories".</summary>
public class RecipientNotificationPreferenceTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(NotificationCategory.Otp)]
    [InlineData(NotificationCategory.SecurityAlert)]
    [InlineData(NotificationCategory.Payment)]
    [InlineData(NotificationCategory.Result)]
    [InlineData(NotificationCategory.Transactional)]
    public void OptOut_is_rejected_for_every_mandatory_category(NotificationCategory category)
    {
        var result = RecipientNotificationPreference.OptOut(Guid.NewGuid(), category, _now);

        Assert.True(result.IsFailure);
        Assert.Equal("notification_preference.mandatory_category", result.Error!.Code);
    }

    [Fact]
    public void OptOut_succeeds_for_the_Informational_category()
    {
        var result = RecipientNotificationPreference.OptOut(Guid.NewGuid(), NotificationCategory.Informational, _now);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.OptedOut);
    }
}
