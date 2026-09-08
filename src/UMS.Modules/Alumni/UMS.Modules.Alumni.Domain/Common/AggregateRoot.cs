namespace UMS.Modules.Alumni.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors every other
/// module's own <c>AggregateRoot{TId}</c> exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) is this module's
/// uniform concurrency mechanism for <c>JobPosting</c> (design-decisions.md "Job-Posting
/// Moderation-Queue Concurrency Control" - mirrors Content's own <c>Notice</c>/<c>Banner</c> and
/// Research's own <c>Grant</c> exactly) and is applied to every other aggregate root here too, for
/// the same reason every other module applies it uniformly rather than inventing a second
/// concurrency mechanism for some aggregates and not others.
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
