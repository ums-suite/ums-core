namespace UMS.Modules.Career.Application.Abstractions;

/// <summary>
/// CAR-16: Career is at least the fifth module to build the "poll another module's outbox by
/// schema-qualified name" pattern for Student specifically (after Hostel, Library, Alumni, Faculty's
/// equivalent) - copies Alumni's own just-built <c>StudentOutboxEventSource</c> shape exactly
/// (Student's own outbox uses EF-default PascalCase naming: <c>student."OutboxMessages"</c>,
/// matched by a <c>LIKE</c> suffix on the event's simple name).
///
/// <para>
/// design-decisions.md "Student-Graduation Boundary for In-Flight Career Activity": consuming EITHER
/// event triggers absolutely NO mutation to any existing <c>CareerApplication</c>/registration - the
/// resulting envelope is acknowledged (<see cref="MarkProcessedAsync"/>) with no further action beyond
/// that, deliberately keeping the handler trivial (no fan-out, no per-application logic). The actual
/// `Student.status = Active` eligibility gate (CAR-6, CAR-11) is a SEPARATE, live read via
/// <c>UMS.Shared.Student.IStudentStatusChecker</c> at the moment of each new submission - never
/// derived from these consumed events.
/// </para>
/// </summary>
public interface IStudentStatusEventSource
{
    public Task<IReadOnlyList<StudentStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <param name="EventId">Student's own outbox message id.</param>
/// <param name="EventType">Either <c>StudentGraduated</c> or <c>StudentStatusChanged</c>.</param>
/// <param name="StudentId">The Student the event concerns.</param>
/// <param name="OccurredAt">Student's own recorded occurrence time.</param>
public sealed record StudentStatusEventEnvelope(Guid EventId, string EventType, Guid StudentId, DateTimeOffset OccurredAt);
