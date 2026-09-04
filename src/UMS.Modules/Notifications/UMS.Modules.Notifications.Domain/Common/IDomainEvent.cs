namespace UMS.Modules.Notifications.Domain.Common;

/// <summary>
/// Marker for one of the events named in requirement-spec.md notifications §3
/// (<c>&lt;Entity&gt;&lt;PastTenseVerb&gt;</c>, ums-conventions.md Naming). These are, per that
/// section, "internal to Notifications' own lifecycle, not generally re-fanned to other modules" -
/// consumed today by NTF-17's own Audit integration call site, not re-published to another module's
/// outbox.
/// </summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAt { get; }
}
