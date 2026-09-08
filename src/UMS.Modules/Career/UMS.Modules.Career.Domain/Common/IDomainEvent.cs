namespace UMS.Modules.Career.Domain.Common;

/// <summary>Marker interface for a domain event raised by an <see cref="AggregateRoot{TId}"/>. Mirrors every other module's own copy exactly.</summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAt { get; }
}
