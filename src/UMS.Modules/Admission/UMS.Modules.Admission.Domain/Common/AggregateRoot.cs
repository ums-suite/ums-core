namespace UMS.Modules.Admission.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors
/// <c>UMS.Modules.Finance.Domain.Common.AggregateRoot{TId}</c> exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) backs every ordinary
/// field update on Admission's aggregates. It is deliberately NOT the mechanism protecting the
/// invariants design-decisions.md calls out as needing something stronger: <c>ExamAttempt</c>'s
/// single-submission race and <c>Application</c>'s duplicate-submit race are both guarded by a
/// state-guarded conditional <c>UPDATE ... WHERE status = @expected</c> issued directly by their
/// repositories (mirroring Academic's own <c>ResultPublicationRepository.TryTransitionAsync</c> and
/// <c>CourseOfferingRepository</c>'s seat-limit mechanism), and the test-slot capacity race (ADM-9)
/// is guarded by an atomic conditional decrement on <c>TestSlot.RemainingSeats</c>, not by
/// <c>Version</c> either - see each aggregate's own remarks.
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
