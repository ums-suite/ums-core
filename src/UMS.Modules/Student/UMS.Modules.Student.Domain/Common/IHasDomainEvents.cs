namespace UMS.Modules.Student.Domain.Common;

/// <summary>Non-generic view of <see cref="AggregateRoot{TId}"/> so infrastructure code can find every tracked aggregate's pending events regardless of id type. Mirrors Faculty's own copy exactly.</summary>
public interface IHasDomainEvents
{
    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    public void ClearDomainEvents();
}
