namespace UMS.Modules.Faculty.Infrastructure.Persistence;

/// <summary>
/// FAC-4: Faculty's own idempotency ledger for inbound `InstructorAssigned`/`InstructorUnassigned`
/// events (see <c>IInstructorAssignmentEventSource</c>'s own remarks) - Faculty tracks "have I
/// applied this event id" itself, in its own schema, rather than writing an acknowledgement back
/// into Academic's outbox table (which would cross module ownership the other way).
/// </summary>
internal sealed class ProcessedInboundEvent
{
    public ProcessedInboundEvent(Guid eventId, DateTimeOffset processedAt)
    {
        EventId = eventId;
        ProcessedAt = processedAt;
    }

    private ProcessedInboundEvent()
    {
    }

    public Guid EventId { get; private set; }

    public DateTimeOffset ProcessedAt { get; private set; }
}
