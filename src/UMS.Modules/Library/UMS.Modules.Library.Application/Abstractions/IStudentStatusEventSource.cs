namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>
/// LIB-16: mirrors Hostel's own <c>IStudentStatusEventSource</c> exactly - built against Student's
/// own outbox table instead of a Library-owned one. See <see cref="StudentStatusEventEnvelope"/>'s
/// own remarks on why only the id, never the decoded status, crosses the module boundary.
/// </summary>
public interface IStudentStatusEventSource
{
    public Task<IReadOnlyList<StudentStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Deliberately carries only <see cref="StudentId"/>, not the event's own <c>NewStatus</c> payload
/// value: Student's own domain event serializes <c>StudentStatus</c> as its raw numeric enum value,
/// and Library cannot reference Student's Domain-owned enum type to decode it (ADR-0002). Re-resolving
/// the CURRENT status via the already-depended-on <c>UMS.Shared.Student.IStudentStatusChecker</c> at
/// processing time avoids a fragile, hand-copied numeric-to-name mapping - and since
/// <c>LoanReviewFlagService</c>'s flag is purely advisory, reading the current status rather than the
/// historical event payload is, if anything, the more useful signal for the librarian reviewing it.
/// </summary>
public sealed record StudentStatusEventEnvelope(Guid EventId, Guid StudentId, DateTimeOffset OccurredAt);
