namespace UMS.Modules.Notifications.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling) - mirrors Identity's and
/// Organization's own <c>AggregateRoot&lt;TId&gt;</c> exactly, kept per-module rather than shared
/// since none of the three built modules so far have promoted it to a shared library (see
/// <c>UMS.Shared.Outbox.OutboxMessage</c>'s own remarks on the "used identically by two or more
/// modules" bar for promotion - this tiny type has not crossed that bar yet).
/// </summary>
public abstract class AggregateRoot<TId> : IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
