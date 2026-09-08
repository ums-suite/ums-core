namespace UMS.Modules.Alumni.Application.Abstractions;

/// <summary>
/// ALM-1: polls Student's own outbox for <c>StudentGraduated</c> - mirrors Research's own
/// <c>IFacultyStatusEventSource</c>/<c>FacultyOutboxEventSource</c> (itself mirroring Library's/
/// Hostel's own Student-outbox pollers), the most recent, correct precedent for this exact
/// cross-module outbox-polling shape: Student's own <c>StudentDbContext</c> uses EF-default
/// PascalCase naming (no explicit <c>OutboxMessageConfiguration</c>), so the physical table is
/// <c>student."OutboxMessages"</c>, quoted PascalCase columns, full-CLR-type-name <c>EventType</c>
/// values matched via a <c>LIKE</c> suffix on <c>StudentGraduated</c>.
/// </summary>
public interface IStudentGraduatedEventSource
{
    public Task<IReadOnlyList<StudentGraduatedEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <param name="EventId">Student's own outbox message id - the idempotency key tracked in Alumni's own <c>processed_inbound_events</c> table (defense-in-depth alongside, never instead of, the DB unique constraint on <c>student_id_ref</c> - design-decisions.md "Idempotent StudentGraduated Consumption").</param>
/// <param name="StudentId">The graduating Student's id - becomes <c>Alumnus.StudentIdRef</c>.</param>
/// <param name="OccurredAt">Student's own event timestamp - the graduation year is derived from this (see <c>Alumnus</c>'s own remarks for why: Student's already-shipped event carries no explicit graduation-year field).</param>
public sealed record StudentGraduatedEventEnvelope(Guid EventId, Guid StudentId, DateTimeOffset OccurredAt);
