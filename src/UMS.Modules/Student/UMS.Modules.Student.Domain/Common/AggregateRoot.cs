namespace UMS.Modules.Student.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Collects the events its
/// own methods raise so the application layer can hand them to the outbox in the same transaction
/// as the state change (ADR-0003), then clear them once persisted. Mirrors
/// <c>UMS.Modules.Faculty.Domain.Common.AggregateRoot{TId}</c> exactly.
/// </summary>
public abstract class AggregateRoot<TId> : IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Optimistic-concurrency token, backed by PostgreSQL's own <c>xmin</c> system column via each
    /// entity's own <c>IEntityTypeConfiguration</c> - see design-decisions.md's "Status-Change
    /// Transactional Boundary" decision.
    /// </summary>
    public uint Version { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
