using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class LoanRepository(LibraryDbContext context) : ILoanRepository
{
    public Task<Loan?> GetByIdAsync(LoanId id, CancellationToken cancellationToken = default) =>
        context.Loans.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    /// <summary>design-decisions.md "Concurrent Return/Renewal Conflict Contract": no `.Include` needed on Loan, so the single-step `FromSqlInterpolated ... FOR UPDATE` form suffices (mirrors Hostel's own `BedRepository`).</summary>
    public Task<Loan?> GetByIdForUpdateAsync(LoanId id, CancellationToken cancellationToken = default) =>
        context.Loans
            .FromSqlInterpolated($"SELECT *, xmin FROM library.loans WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    /// <summary>LIB-17: locks by BookCopyId rather than a known LoanId - a raw lock on the candidate row(s), then a normal tracked LINQ re-query for the specific Active one (mirrors Hostel's own two-step `HostelApplicationRepository.GetByIdForUpdateAsync` pattern, applied here because the caller does not yet know the LoanId up front).</summary>
    public async Task<Loan?> GetActiveByBookCopyForUpdateAsync(Guid bookCopyId, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM library.loans WHERE book_copy_id = {bookCopyId} AND status = 'Active' FOR UPDATE", cancellationToken).ConfigureAwait(false);
        return await context.Loans.FirstOrDefaultAsync(l => l.BookCopyId == bookCopyId && l.Status == LoanStatus.Active, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Loan>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        await context.Loans.Where(l => l.BorrowerId == borrowerId).OrderByDescending(l => l.IssuedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> CountActiveByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        context.Loans.CountAsync(l => l.BorrowerId == borrowerId && l.Status == LoanStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<Loan>> GetOverdueActiveAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Loans
            .Where(l => l.Status == LoanStatus.Active && l.DueDate < asOf)
            .OrderBy(l => l.DueDate)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Loan loan) => context.Loans.Add(loan);
}
