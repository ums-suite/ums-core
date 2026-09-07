namespace UMS.Modules.Admission.Application.Abstractions;

/// <summary>
/// design-decisions.md "Duplicate payment webhook racing the application-lock transaction": the
/// read/ack side of Finance's OWN outbox (<c>finance.outbox_messages</c>), read directly by
/// schema-qualified name - the identical first-of-its-kind cross-module-outbox-polling pattern
/// Faculty's own <c>IInstructorAssignmentEventSource</c> established for Academic's outbox (see
/// that interface's own remarks). Finance itself never calls back into Admission (module-
/// boundaries.md: "Finance intentionally has no outgoing domain dependency") - Admission is the
/// one reading Finance's own `PaymentSucceeded`/`PaymentFailed` events, matching every event to
/// one of its own Applications by <c>InvoiceId</c> (an event whose InvoiceId matches neither this
/// Application's ApplicationFeeInvoiceId nor its ConfirmationFeeInvoiceId belongs to a different
/// module entirely - Student/Hostel/Library/Alumni's own Finance usage - and is simply skipped).
/// Acknowledgement is tracked in Admission's OWN <c>processed_inbound_events</c> table, never a
/// write into Finance's schema.
/// </summary>
public interface IFinancePaymentEventSource
{
    public Task<IReadOnlyList<FinancePaymentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <param name="EventId">Finance's own outbox message id - the idempotency key tracked in Admission's own <c>processed_inbound_events</c> table.</param>
/// <param name="EventType"><c>PaymentSucceeded</c> or <c>PaymentFailed</c>.</param>
/// <param name="InvoiceId">Matched against <c>Application.ApplicationFeeInvoiceId</c>/<c>ConfirmationFeeInvoiceId</c>.</param>
public sealed record FinancePaymentEventEnvelope(Guid EventId, string EventType, Guid InvoiceId, DateTimeOffset OccurredAt);
