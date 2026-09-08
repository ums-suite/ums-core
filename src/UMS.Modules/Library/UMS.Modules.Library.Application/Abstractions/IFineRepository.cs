using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Application.Abstractions;

public interface IFineRepository
{
    public Task<Fine?> GetByIdAsync(FineId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Fine-Accrual Job Idempotency" / "Fine-Settlement Consistency": the pessimistic lock every Fine writer (accrual, settlement, waiver) takes. Uses the two-step lock-then-query pattern (see the Infrastructure implementation's own remarks) - composing `FromSqlInterpolated` directly against Fine breaks its `ComplexProperty`-mapped Amount field's column shaping.</summary>
    public Task<Fine?> GetByIdForUpdateAsync(FineId id, CancellationToken cancellationToken = default);

    public Task<Fine?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>LIB-11: the currently-open (still Accruing) Overdue Fine for a Loan, if any - a later day's accrual increments this same row rather than creating a second one.</summary>
    public Task<Fine?> GetOpenOverdueFineForUpdateAsync(Guid loanId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Fine>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Fine-Blocks-New-Loan Enforcement Point": Accruing or PendingSettlement counts as unpaid - only Paid/Waived clears a borrower to take a new Loan.</summary>
    public Task<bool> HasUnsettledFineAsync(Guid borrowerId, CancellationToken cancellationToken = default);

    public void Add(Fine fine);
}
