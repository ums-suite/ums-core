namespace UMS.Modules.Notifications.Domain.Common;

/// <summary>
/// design-decisions.md, "Bulk-Burst Prioritized Dispatch - Three-Tier Queue Model": Expedited
/// (OTP/security, §9 Decision 4) sits above both Standard and Bulk unconditionally; Bulk is
/// throttled against Standard's own capacity so a result-publication burst never starves ordinary
/// sends.
/// </summary>
public enum NotificationPriority
{
    Expedited = 0,
    Standard = 1,
    Bulk = 2,
}
