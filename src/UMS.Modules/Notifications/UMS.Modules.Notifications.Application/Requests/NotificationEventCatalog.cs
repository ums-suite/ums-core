using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Application.Requests;

/// <summary>
/// requirement-spec.md §2 Event Catalog Consumed table (BRD §12, §26) - the default channel set and
/// category for every named trigger category. §9 Open Questions is explicit that "the full,
/// enumerated template catalog ... is deferred to a data-driven, admin-configurable registry" - this
/// catalog is that mechanism's seed data (a starting default), not a closed, compile-time-only list:
/// an event type absent from this table still gets accepted (NTF-3 never rejects an unknown event
/// type), just with a conservative default (Standard priority, Transactional category, In-App only)
/// until an operator configures it for real via NTF-8's template management once a real publishing
/// module exists to name its own event types precisely.
/// </summary>
public static class NotificationEventCatalog
{
    private static readonly Dictionary<string, NotificationEventDefaults> _entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ApplicationSubmitted"] = new(NotificationCategory.Transactional, NotificationPriority.Standard, [NotificationChannel.Email, NotificationChannel.InApp]),
        ["PaymentCompleted"] = new(NotificationCategory.Payment, NotificationPriority.Standard, [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.InApp]),
        ["AdmitCardReady"] = new(NotificationCategory.Transactional, NotificationPriority.Standard, [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.InApp]),
        ["ResultPublished"] = new(NotificationCategory.Result, NotificationPriority.Bulk, [NotificationChannel.Sms, NotificationChannel.InApp]),
        ["GradePublished"] = new(NotificationCategory.Transactional, NotificationPriority.Standard, [NotificationChannel.InApp, NotificationChannel.Email]),
        ["FeeDue"] = new(NotificationCategory.Informational, NotificationPriority.Standard, [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.InApp]),
        ["HostelAllocation"] = new(NotificationCategory.Transactional, NotificationPriority.Standard, [NotificationChannel.Email, NotificationChannel.Sms, NotificationChannel.InApp]),
        ["LeaveApproval"] = new(NotificationCategory.Transactional, NotificationPriority.Standard, [NotificationChannel.InApp, NotificationChannel.Email]),

        // §9 Decision 4: OTP/security-alert messages get an expedited priority lane, ahead of bulk/
        // informational sends - WhatsApp included per §9 Open Questions ("a first-class delivery
        // channel for urgent/OTP-class messages").
        ["OtpIssued"] = new(NotificationCategory.Otp, NotificationPriority.Expedited, [NotificationChannel.Sms, NotificationChannel.WhatsApp]),
        ["SecurityAlertRaised"] = new(NotificationCategory.SecurityAlert, NotificationPriority.Expedited, [NotificationChannel.Sms, NotificationChannel.Email]),
    };

    private static readonly NotificationEventDefaults _fallback = new(NotificationCategory.Transactional, NotificationPriority.Standard, [NotificationChannel.InApp]);

    public static NotificationEventDefaults Resolve(string eventType) =>
        _entries.TryGetValue(eventType, out var defaults) ? defaults : _fallback;
}
