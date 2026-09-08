namespace UMS.Modules.Hostel.Application.Abstractions;

/// <summary>
/// HOS-9: mirrors Admission's own <c>IFinancePaymentEventSource</c>/<c>FinanceOutboxEventSource</c>
/// exactly (the platform's own precedent for polling Finance's outbox table by schema-qualified
/// name) - the mechanism analogous "PaymentCompleted" resolves to in code is Finance's actual
/// <c>PaymentSucceeded</c>/<c>PaymentFailed</c> domain events (requirement-spec.md §3's own naming
/// reconciliation note).
/// </summary>
public interface IFinancePaymentEventSource
{
    public Task<IReadOnlyList<FinancePaymentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="InvoiceId"/> is the one field both <c>PaymentSucceeded</c> and <c>PaymentFailed</c>
/// carry - <c>PaymentFailed</c> has no <c>SourceModule</c>/<c>SourceReferenceId</c> field at all, so
/// (mirroring Admission's own precedent exactly) filtering to "is this a hostel event" happens by
/// matching <see cref="InvoiceId"/> against Hostel's own <c>Allocation.InvoiceId</c> in
/// <c>AllocationFeeConfirmationService</c>, never by a SQL-level <c>SourceModule</c> filter.
/// </summary>
public sealed record FinancePaymentEventEnvelope(Guid EventId, string EventType, Guid InvoiceId, DateTimeOffset OccurredAt);
