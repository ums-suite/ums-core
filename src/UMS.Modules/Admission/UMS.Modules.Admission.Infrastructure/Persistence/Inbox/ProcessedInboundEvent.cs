namespace UMS.Modules.Admission.Infrastructure.Persistence.Inbox;

/// <summary>Admission's own idempotency ledger for inbound Finance `PaymentSucceeded`/`PaymentFailed` events (see <c>IFinancePaymentEventSource</c>'s own remarks) - Admission tracks "have I applied this event id" itself, in its own schema, rather than writing an acknowledgement back into Finance's outbox table.</summary>
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
