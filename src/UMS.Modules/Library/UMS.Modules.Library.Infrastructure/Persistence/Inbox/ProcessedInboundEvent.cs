namespace UMS.Modules.Library.Infrastructure.Persistence.Inbox;

/// <summary>
/// Library's own idempotency ledger for every inbound cross-module event source it polls -
/// Finance's <c>PaymentSucceeded</c>/<c>PaymentFailed</c> (LIB-13), Student's
/// <c>StudentStatusChanged</c> (LIB-16), and Faculty's <c>FacultyMemberStatusChanged</c> (LIB-16) -
/// distinguished by <see cref="Source"/> so the three id spaces can never collide. Mirrors Hostel's
/// own <c>ProcessedInboundEvent</c> exactly, extended to a third source.
/// </summary>
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
