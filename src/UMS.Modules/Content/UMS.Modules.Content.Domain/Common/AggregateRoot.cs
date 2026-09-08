namespace UMS.Modules.Content.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors every other
/// module's own <c>AggregateRoot{TId}</c> exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) is Content's ONLY
/// concurrency mechanism (design-decisions.md "Concurrent-Edit Conflict Resolution") - every writer
/// of <see cref="Notices.Notice"/>/<see cref="Banners.Banner"/> (a human Admin's save, a concurrent
/// Admin's save, and the scheduled publish/expire job's own state-machine transition) is an
/// equal-standing writer subject to the identical version-check-and-reject rule. Unlike Hostel's
/// <c>Allocation</c>/Finance's <c>Payment</c>, Content deliberately has NO pessimistic
/// <c>SELECT ... FOR UPDATE</c> locking anywhere - this module has no oversell-shaped invariant that
/// needs one.
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
