using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class FineRepository(LibraryDbContext context) : IFineRepository
{
    private static readonly FineStatus[] UnsettledStatuses = [FineStatus.Accruing, FineStatus.PendingSettlement];

    public Task<Fine?> GetByIdAsync(FineId id, CancellationToken cancellationToken = default) =>
        context.Fines.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    /// <summary>design-decisions.md "Fine-Accrual Job Idempotency" / "Fine-Settlement Consistency": no `.Include` needed on Fine, so the single-step `FromSqlInterpolated ... FOR UPDATE` form suffices.</summary>
    public Task<Fine?> GetByIdForUpdateAsync(FineId id, CancellationToken cancellationToken = default) =>
        context.Fines
            .FromSqlInterpolated($"SELECT *, xmin FROM library.fines WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Fine?> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
        context.Fines.FirstOrDefaultAsync(f => f.InvoiceId == invoiceId, cancellationToken);

    public async Task<Fine?> GetOpenOverdueFineForUpdateAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM library.fines WHERE loan_id = {loanId} AND reason = 'Overdue' AND status = 'Accruing' FOR UPDATE", cancellationToken).ConfigureAwait(false);
        return await context.Fines.FirstOrDefaultAsync(f => f.LoanId == loanId && f.Reason == FineReason.Overdue && f.Status == FineStatus.Accruing, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Fine>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        await context.Fines.Where(f => f.BorrowerId == borrowerId).OrderByDescending(f => f.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> HasUnsettledFineAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        context.Fines.AnyAsync(f => f.BorrowerId == borrowerId && UnsettledStatuses.Contains(f.Status), cancellationToken);

    public void Add(Fine fine) => context.Fines.Add(fine);
}
