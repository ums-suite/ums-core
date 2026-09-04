namespace UMS.Modules.Organization.Domain.Common;

/// <summary>
/// Non-generic view of <see cref="AggregateRoot{TId}"/> so infrastructure code (EF Core's
/// <c>ChangeTracker</c>) can find every tracked aggregate's pending events with one query,
/// regardless of each aggregate's own id type. Mirrors Identity's own copy exactly.
/// </summary>
public interface IHasDomainEvents
{
    public IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    public void ClearDomainEvents();
}
