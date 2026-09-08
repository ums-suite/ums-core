namespace UMS.Modules.Alumni.Application.Abstractions;

/// <summary>
/// ALM-9: the read/ack side of Finance's OWN outbox (<c>finance.outbox_messages</c>), read directly
/// by schema-qualified name - mirrors Admission's own <c>IFinancePaymentEventSource</c>/
/// <c>FinanceOutboxEventSource</c> exactly (short event names <c>PaymentSucceeded</c>/
/// <c>PaymentFailed</c>, snake_case columns - the third distinct outbox wire shape in this codebase,
/// after Student/Faculty's PascalCase and this same Finance shape Admission/Hostel/Library already
/// consume). Finance itself never calls back into Alumni (module-boundaries.md: "Finance
/// intentionally has no outgoing domain dependency"). Acknowledgement is tracked in Alumni's OWN
/// <c>processed_inbound_events</c> table, never a write into Finance's schema.
/// </summary>
public interface IFinancePaymentEventSource
{
    public Task<IReadOnlyList<FinancePaymentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <param name="EventId">Finance's own outbox message id - the idempotency key tracked in Alumni's own <c>processed_inbound_events</c> table.</param>
/// <param name="EventType"><c>PaymentSucceeded</c> or <c>PaymentFailed</c>.</param>
/// <param name="InvoiceId">Matched against <c>Donation.InvoiceId</c> - an event matching no Donation belongs to a different module's own Finance usage and is simply skipped.</param>
public sealed record FinancePaymentEventEnvelope(Guid EventId, string EventType, Guid InvoiceId, DateTimeOffset OccurredAt);
