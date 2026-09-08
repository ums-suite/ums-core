namespace UMS.Modules.Hostel.Application.Abstractions;

/// <summary>
/// HOS-17: Hostel is the FIRST cross-module consumer of Student's own <c>StudentStatusChanged</c>
/// event - the equivalent of Admission's <c>IFinancePaymentEventSource</c>, built against Student's
/// own outbox table instead of Finance's, mirroring the exact same raw-SQL, schema-qualified,
/// own-inbox-ledger pattern (and Faculty's own even earlier <c>IInstructorAssignmentEventSource</c>
/// precedent against Academic's outbox).
/// </summary>
public interface IStudentStatusEventSource
{
    public Task<IReadOnlyList<StudentStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deliberately carries only <see cref="StudentId"/>, not the event's own <c>NewStatus</c> payload
/// value: Student's own domain event serializes <c>StudentStatus</c> as its raw numeric enum value
/// (no <c>JsonStringEnumConverter</c>), and Hostel cannot reference Student's Domain-owned enum type
/// to decode it (ADR-0002 module isolation - Infrastructure has no project reference to Student's
/// Domain/Application). Re-resolving the CURRENT status via the already-depended-on
/// <c>UMS.Shared.Student.IStudentStatusChecker</c> at processing time avoids a fragile, hand-copied
/// numeric-to-name mapping entirely - and since this flag is purely advisory (design-decisions.md),
/// reading the current status rather than the historical event payload is, if anything, the more
/// useful signal for the Officer reviewing it.
/// </summary>
public sealed record StudentStatusEventEnvelope(Guid EventId, Guid StudentId, DateTimeOffset OccurredAt);
