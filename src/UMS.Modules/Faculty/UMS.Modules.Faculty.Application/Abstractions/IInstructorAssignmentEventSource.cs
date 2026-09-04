namespace UMS.Modules.Faculty.Application.Abstractions;

/// <summary>
/// FAC-4: the read/ack side of the *inbound* half of Academic's `InstructorAssigned`/
/// `InstructorUnassigned` events (requirement-spec.md faculty §2 Course Assignment, §6, §9's first
/// Decision). This is a first-of-its-kind pattern in this codebase: every existing outbox relay
/// (Audit's export relay, Documents' bulk/retry relays) reads its OWN module's outbox table -
/// nothing today reads a *different* module's outbox. Since Academic (release/
/// DEVELOPMENT_PLAN.md Flow #12) doesn't exist yet, there is no real `academic.outbox_messages`
/// table to read from in production today; the real implementation
/// (<c>AcademicOutboxEventSource</c>, Infrastructure) is written to poll it by schema-qualified
/// name once Academic lands, following the shared <c>UMS.Shared.Outbox.OutboxMessage</c> row
/// shape/serialization convention that class's own doc comment establishes as shared across
/// modules even though each module keeps its own physical table (ADR-0001: one physical
/// database). Acknowledgement is tracked in Faculty's OWN <c>processed_inbound_events</c> table
/// (never a write into Academic's schema - Faculty only ever reads it, preserving module
/// ownership) so a message already applied is never re-applied even though it can never be
/// "marked processed" in the producer's own row the way an in-module relay would.
/// </summary>
public interface IInstructorAssignmentEventSource
{
    public Task<IReadOnlyList<InstructorAssignmentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>One inbound instructor-assignment event, as read from Academic's outbox.</summary>
/// <param name="EventId">Academic's outbox message id - the idempotency key tracked in Faculty's own <c>processed_inbound_events</c> table.</param>
/// <param name="EventType">`InstructorAssigned` or `InstructorUnassigned`.</param>
/// <param name="PayloadJson">Deserializes to <c>UMS.Modules.Faculty.Application.CourseAssignments.InstructorAssignmentPayload</c>.</param>
/// <param name="OccurredAt">Academic's own event timestamp - the ordering guard <c>CourseAssignment.ApplyAssigned</c>/<c>ApplyUnassigned</c> compares against (design-decisions.md).</param>
public sealed record InstructorAssignmentEventEnvelope(Guid EventId, string EventType, string PayloadJson, DateTimeOffset OccurredAt);
