using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.UnitTests.Requests;

/// <summary>requirement-spec.md §2's BRD §12/§26 event catalog table.</summary>
public class NotificationEventCatalogTests
{
    [Fact]
    public void Resolve_maps_ResultPublished_to_Result_category_and_Bulk_priority()
    {
        var defaults = NotificationEventCatalog.Resolve("ResultPublished");

        Assert.Equal(NotificationCategory.Result, defaults.Category);
        Assert.Equal(NotificationPriority.Bulk, defaults.Priority);
        Assert.Contains(NotificationChannel.Sms, defaults.Channels);
        Assert.Contains(NotificationChannel.InApp, defaults.Channels);
    }

    [Fact]
    public void Resolve_maps_OtpIssued_to_Otp_category_and_Expedited_priority()
    {
        var defaults = NotificationEventCatalog.Resolve("OtpIssued");

        Assert.Equal(NotificationCategory.Otp, defaults.Category);
        Assert.Equal(NotificationPriority.Expedited, defaults.Priority);
    }

    [Fact]
    public void Resolve_falls_back_to_a_conservative_default_for_an_unknown_event_type()
    {
        var defaults = NotificationEventCatalog.Resolve("SomeFutureModuleEventNobodyRegisteredYet");

        Assert.Equal(NotificationCategory.Transactional, defaults.Category);
        Assert.Equal(NotificationPriority.Standard, defaults.Priority);
        Assert.Equal([NotificationChannel.InApp], defaults.Channels);
    }
}
