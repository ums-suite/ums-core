namespace UMS.Modules.Notifications.Domain.Common;

public static class NotificationCategoryExtensions
{
    public static bool IsMandatory(this NotificationCategory category) => category != NotificationCategory.Informational;

    /// <summary>OTP/security-alert categories get the Expedited queue tier and paging dead-letter alerts (§9 Decision 4; design-decisions.md "Dead-Letter Alerting Tier").</summary>
    public static bool IsExpeditedByDefault(this NotificationCategory category) =>
        category is NotificationCategory.Otp or NotificationCategory.SecurityAlert;

    /// <summary>requirement-spec.md §5 NFR Auditability: "OTP, security-alert, payment, and result-category notifications are audited" - Transactional/Informational are not (NTF-17).</summary>
    public static bool IsAuditRequired(this NotificationCategory category) =>
        category is NotificationCategory.Otp or NotificationCategory.SecurityAlert or NotificationCategory.Payment or NotificationCategory.Result;
}
