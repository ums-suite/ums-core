namespace UMS.Modules.Reporting.Domain.Common;

/// <summary>A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors every other module's own <c>AggregateRoot{TId}</c> exactly - the established per-module duplication convention, not something to unify.</summary>
public abstract class AggregateRoot<TId> : IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    public uint Version { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
