namespace UMS.Modules.Notifications.Domain.Common;

/// <summary>The five delivery channels (requirement-spec.md §3 Module-Local Terms, plus WhatsApp per §9 Open Questions - "a fifth channel ... a first-class delivery channel for urgent/OTP-class messages").</summary>
public enum NotificationChannel
{
    Email = 0,
    Sms = 1,
    WhatsApp = 2,
    Push = 3,
    InApp = 4,
}
