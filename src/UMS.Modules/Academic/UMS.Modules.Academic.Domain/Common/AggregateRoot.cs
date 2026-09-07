namespace UMS.Modules.Academic.Domain.Common;

/// <summary>
/// A behavior-bearing aggregate root (ums-conventions.md, Domain Modeling). Mirrors
/// <c>UMS.Modules.Faculty.Domain.Common.AggregateRoot{TId}</c> / Student's own copy exactly.
///
/// <para>
/// <see cref="Version"/> (Postgres <c>xmin</c>-backed optimistic concurrency) is the default for
/// every Academic aggregate EXCEPT the two design-decisions.md explicitly calls out as needing a
/// different mechanism: <c>CourseOffering.EnrolledCount</c> (seat-limit) and
/// <c>ResultPublication.Status</c> (the grade-lock state machine) are mutated via a
/// state-guarded/conditional <c>ExecuteUpdateAsync</c> at the repository layer instead - see those
/// aggregates' own remarks. Every other aggregate here (Program, Curriculum, Course,
/// AcademicSession, CourseOffering's non-counter fields, AttendanceSession) uses this class's
/// ordinary <c>xmin</c> path, matching the rest of this codebase's default.
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
