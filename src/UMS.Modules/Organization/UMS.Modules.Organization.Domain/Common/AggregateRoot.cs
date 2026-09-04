namespace UMS.Modules.Organization.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling: "invariants are
/// enforced by methods on the aggregate itself ... never by an external application service
/// reading and mutating public setters"). Collects the events its own methods raise so the
/// application layer can hand them to the outbox in the same transaction as the state change
/// (ADR-0003), then clear them once persisted. Mirrors
/// <c>UMS.Modules.Identity.Domain.Common.AggregateRoot{TId}</c> exactly.
/// </summary>
public abstract class AggregateRoot<TId> : IHasDomainEvents
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public TId Id { get; protected set; } = default!;

    /// <summary>
    /// Optimistic-concurrency token (design-decisions.md, "Optimistic Concurrency (Version
    /// Column) for Hierarchy Edits"; edge-cases.md "Concurrent edits to the same
    /// Department/Program"). Backed by PostgreSQL's own <c>xmin</c> system column via each
    /// entity's own <c>IEntityTypeConfiguration</c> (idiomatic EF Core + Npgsql concurrency
    /// support - no hand-rolled version-increment logic needed) rather than an
    /// application-managed integer, so it is *always* correct even for a write path that bypasses
    /// the aggregate's own methods (there is none today, but the DB-enforced token means that
    /// could never silently break this invariant). EF Core sets this value on load/insert/update;
    /// application code only ever reads it back to hand to the client and compares it against
    /// what a client resubmits on `PATCH`.
    /// </summary>
    public uint Version { get; protected set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
