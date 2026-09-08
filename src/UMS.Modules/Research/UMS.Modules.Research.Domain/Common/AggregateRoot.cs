namespace UMS.Modules.Research.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors every other
/// module's own <c>AggregateRoot{TId}</c> exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) is Research's ONLY
/// concurrency mechanism for <c>Grant</c> (design-decisions.md "Grant and Investigator-List
/// Concurrency Control") - mirrors Faculty's own <c>LeaveRequest</c> exactly. Applied uniformly to
/// every state-changing <c>Grant</c> endpoint (lifecycle transitions AND investigator add/remove),
/// never a pessimistic lock.
/// </para>
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
