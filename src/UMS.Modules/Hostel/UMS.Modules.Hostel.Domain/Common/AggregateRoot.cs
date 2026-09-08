namespace UMS.Modules.Hostel.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors
/// <c>UMS.Modules.Finance.Domain.Common.AggregateRoot{TId}</c> exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) backs ordinary field
/// updates. It is deliberately NOT the mechanism protecting the invariants design-decisions.md calls
/// out as needing something stronger - Bed-allocation's oversell race and the
/// <c>HostelApplication</c> withdrawal-vs-approval race are both guarded by an explicit
/// <c>SELECT ... FOR UPDATE</c> pessimistic row lock taken by the repository (mirroring Finance's
/// own <c>Payment</c>/<c>Invoice</c> lock pattern), never by <see cref="Version"/> - see each
/// aggregate's own remarks.
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
