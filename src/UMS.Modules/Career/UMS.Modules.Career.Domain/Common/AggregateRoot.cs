namespace UMS.Modules.Career.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors every other
/// module's own <c>AggregateRoot{TId}</c> exactly, including the uniform Postgres <c>xmin</c>-backed
/// optimistic-concurrency <see cref="Version"/> column applied to every aggregate root in this module.
/// </summary>
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
