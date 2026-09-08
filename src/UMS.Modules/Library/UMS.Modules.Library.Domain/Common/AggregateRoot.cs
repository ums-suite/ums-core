namespace UMS.Modules.Library.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors
/// <c>UMS.Modules.Hostel.Domain.Common.AggregateRoot{TId}</c> (itself mirroring Finance's own copy)
/// exactly - this is the established per-module duplication convention, not something to unify.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) backs ordinary field
/// updates. It is deliberately NOT the mechanism protecting the invariants design-decisions.md calls
/// out as needing something stronger - copy-issuance's oversell race and the Loan
/// renew-vs-return race are both guarded by an explicit <c>SELECT ... FOR UPDATE</c> pessimistic row
/// lock taken by the repository, never by <see cref="Version"/> - see each aggregate's own remarks.
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
