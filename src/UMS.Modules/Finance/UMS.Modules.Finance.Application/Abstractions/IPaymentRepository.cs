using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.Application.Abstractions;

public interface IPaymentRepository
{
    public Task<Payment?> GetByIdAsync(PaymentId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md's ordering-guarded state-transition function runs under this lock - both the live webhook handler and the stuck-payment polling job acquire it (edge-cases.md "Stuck-Pending Polling Job Racing a Webhook That Arrives Mid-Poll").</summary>
    public Task<Payment?> GetByIdForUpdateAsync(PaymentId id, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §4's IdempotencyKey dedup check, and the partial-unique-index-backed non-terminal-Payment-per-Invoice guard (design-decisions.md "Invoice-Level Concurrency Control").</summary>
    public Task<Payment?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    public Task<Payment?> GetNonTerminalByInvoiceAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>FIN-9/FIN-10: every Payment still non-terminal whose transaction was last updated before <paramref name="updatedBefore"/> - the polling/stale-timeout sweep's own candidate set.</summary>
    public Task<IReadOnlyList<Payment>> GetNonTerminalUpdatedBeforeAsync(DateTimeOffset updatedBefore, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// FIN-14: design-decisions.md "Reconciliation Job Concurrency-Safety" - the daily reconciliation
    /// job's own unlocked candidate scan, mirroring <see cref="GetNonTerminalUpdatedBeforeAsync"/>
    /// exactly: every Successful (not yet Reconciled) Payment last updated before the buffer-window
    /// cutoff, deferring anything more recent to the next run.
    /// </summary>
    public Task<IReadOnlyList<Payment>> GetSuccessfulUnreconciledUpdatedBeforeAsync(DateTimeOffset updatedBefore, int batchSize, CancellationToken cancellationToken = default);

    public void Add(Payment payment);
}
