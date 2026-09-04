namespace UMS.Modules.Notifications.Domain.Common;

/// <summary>
/// requirement-spec.md §9 Decision 3: "category-level opt-out ... mandatory transactional vs.
/// optional informational categories". Every category except <see cref="Informational"/> is
/// mandatory - a recipient can never opt out of an OTP, security alert, payment confirmation, or
/// result notification (see <see cref="NotificationCategoryExtensions.IsMandatory"/>).
/// </summary>
public enum NotificationCategory
{
    /// <summary>Expedited-priority by construction (§9 Decision 4) - never opt-out-able, always audited (NTF-17).</summary>
    Otp = 0,

    /// <summary>Expedited-priority by construction - never opt-out-able, always audited (NTF-17).</summary>
    SecurityAlert = 1,

    /// <summary>Mandatory transactional - never opt-out-able, always audited (NTF-17).</summary>
    Payment = 2,

    /// <summary>Mandatory transactional - never opt-out-able, always audited (NTF-17); typically Bulk-priority on result-publication day.</summary>
    Result = 3,

    /// <summary>Ordinary mandatory transactional (application submitted, admit card ready, hostel allocation, leave approval, ...) - never opt-out-able.</summary>
    Transactional = 4,

    /// <summary>The one opt-out-able category (fee-due reminders, notices, ...).</summary>
    Informational = 5,
}
