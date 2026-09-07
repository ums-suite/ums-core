namespace UMS.Modules.Learning.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors
/// <c>UMS.Modules.Academic.Domain.Common.AggregateRoot{TId}</c> exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) is the default for
/// every Learning aggregate. Note what is deliberately absent: there is NO seat-limit-style atomic
/// conditional-update path anywhere in this module. design-decisions.md's "Why Submission Timing
/// Doesn't Need Seat-Limit-Style Concurrency Control" resolves that a <c>Submission</c>'s accept
/// check contends over nothing - no shared counter, no capacity - so it is an ordinary,
/// unserialized insert, and reusing Academic's <c>CourseOffering.EnrolledCount</c> mechanism here
/// would be solving a problem this module structurally does not have.
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
