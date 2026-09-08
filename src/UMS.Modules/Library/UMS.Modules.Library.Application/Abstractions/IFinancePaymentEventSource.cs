namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>
/// LIB-13: mirrors Admission's/Hostel's own <c>IFinancePaymentEventSource</c>/<c>FinanceOutboxEventSource</c>
/// exactly - the platform's own precedent for polling Finance's outbox table by schema-qualified
/// name. requirement-spec.md §3's "PaymentCompleted" resolves to Finance's actual
/// <c>PaymentSucceeded</c>/<c>PaymentFailed</c> domain events.
/// </summary>
public interface IFinancePaymentEventSource
{
    public Task<IReadOnlyList<FinancePaymentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
}

/// <summary><see cref="InvoiceId"/> is the one field both <c>PaymentSucceeded</c> and <c>PaymentFailed</c> carry - filtering to "is this a library event" happens by matching <see cref="InvoiceId"/> against Library's own <c>Fine.InvoiceId</c> in <c>FinePaymentConfirmationService</c>, never a SQL-level <c>SourceModule</c> filter (mirrors Admission's/Hostel's own precedent exactly).</summary>
public sealed record FinancePaymentEventEnvelope(Guid EventId, string EventType, Guid InvoiceId, DateTimeOffset OccurredAt);
