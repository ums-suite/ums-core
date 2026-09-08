namespace UMS.Modules.Research.Infrastructure.Persistence.Inbox;

/// <summary>Research's own idempotency ledger for Faculty's <c>FacultyMemberStatusChanged</c> outbox (RES-5). Mirrors Library's own <c>ProcessedInboundEvent</c> exactly.</summary>
internal sealed class ProcessedInboundEvent
{
    public ProcessedInboundEvent(Guid eventId, string source, DateTimeOffset processedAt)
    {
        EventId = eventId;
        Source = source;
        ProcessedAt = processedAt;
    }

    private ProcessedInboundEvent()
    {
    }

    public Guid EventId { get; private set; }

    public string Source { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private set; }
}
