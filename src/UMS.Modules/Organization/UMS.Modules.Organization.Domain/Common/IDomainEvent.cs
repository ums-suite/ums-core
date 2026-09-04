namespace UMS.Modules.Organization.Domain.Common;

/// <summary>
/// Marker for one of the events named in requirement-spec.md organization §3
/// (<c>&lt;Entity&gt;&lt;PastTenseVerb&gt;</c>, ums-conventions.md Naming). Mirrors
/// <c>UMS.Modules.Identity.Domain.Common.IDomainEvent</c> exactly - each module owns its own copy
/// rather than sharing one (module-boundaries.md: no module's Domain may reference another's).
/// </summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAt { get; }
}
