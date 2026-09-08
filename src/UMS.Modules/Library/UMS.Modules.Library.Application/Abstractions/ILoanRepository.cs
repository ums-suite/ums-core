using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Application.Abstractions;

public interface ILoanRepository
{
    public Task<Loan?> GetByIdAsync(LoanId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Concurrent Return/Renewal Conflict Contract" / "Copy-Issuance Concurrency Control Pattern": the pessimistic lock <c>Renew</c>/<c>Return</c> both take on the SAME Loan row. Loan has no owned collection needing `.Include`, so the single-step `FromSqlInterpolated ... FOR UPDATE` form suffices.</summary>
    public Task<Loan?> GetByIdForUpdateAsync(LoanId id, CancellationToken cancellationToken = default);

    /// <summary>LIB-17: the write-off flow's own lock - symmetric with <see cref="GetByIdForUpdateAsync"/>, keyed by BookCopy instead of Loan id.</summary>
    public Task<Loan?> GetActiveByBookCopyForUpdateAsync(Guid bookCopyId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Loan>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default);

    public Task<int> CountActiveByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default);

    /// <summary>LIB-10/LIB-11: the overdue-detection and fine-accrual jobs' shared candidate query - every Active Loan whose due date has passed.</summary>
    public Task<IReadOnlyList<Loan>> GetOverdueActiveAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    public void Add(Loan loan);
}
