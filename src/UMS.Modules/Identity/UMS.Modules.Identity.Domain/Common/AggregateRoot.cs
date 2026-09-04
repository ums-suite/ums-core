namespace UMS.Modules.Identity.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling: "invariants are
/// enforced by methods on the aggregate itself ... never by an external application service
/// reading and mutating public setters"). Collects the events its own methods raise so the
/// application layer can hand them to the outbox in the same transaction as the state change
/// (ADR-0003), then clear them once persisted.
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
