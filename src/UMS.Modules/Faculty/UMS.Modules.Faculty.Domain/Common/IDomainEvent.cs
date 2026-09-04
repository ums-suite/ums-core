namespace UMS.Modules.Faculty.Domain.Common;

/// <summary>Marker for one of the events named in requirement-spec.md faculty §3. Each module owns its own copy rather than sharing one (module-boundaries.md).</summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAt { get; }
}
