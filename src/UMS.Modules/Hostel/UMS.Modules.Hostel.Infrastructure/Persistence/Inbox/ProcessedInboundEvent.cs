namespace UMS.Modules.Hostel.Infrastructure.Persistence.Inbox;

/// <summary>
/// Hostel's own idempotency ledger for BOTH inbound cross-module event sources it polls - Finance's
/// <c>PaymentSucceeded</c>/<c>PaymentFailed</c> (HOS-9) and Student's <c>StudentStatusChanged</c>
/// (HOS-17) - distinguished by <see cref="Source"/> so the two id spaces can never collide. Mirrors
/// Admission's own single-source <c>ProcessedInboundEvent</c>, extended with a source discriminator
/// since Hostel is the first module to poll two different modules' outboxes.
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
