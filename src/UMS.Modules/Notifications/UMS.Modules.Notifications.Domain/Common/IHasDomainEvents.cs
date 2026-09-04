namespace UMS.Modules.Notifications.Domain.Common;

/// <summary>Non-generic view of <see cref="AggregateRoot{TId}"/> so infrastructure code (EF Core's <c>ChangeTracker</c>) can find every tracked aggregate's pending events with one query.</summary>
public interface IHasDomainEvents
{
    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    public void ClearDomainEvents();
}
