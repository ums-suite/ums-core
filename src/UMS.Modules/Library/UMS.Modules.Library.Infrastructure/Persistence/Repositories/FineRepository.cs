using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class FineRepository(LibraryDbContext context) : IFineRepository
{
    private static readonly FineStatus[] UnsettledStatuses = [FineStatus.Accruing, FineStatus.PendingSettlement];

    public Task<Fine?> GetByIdAsync(FineId id, CancellationToken cancellationToken = default) =>
        context.Fines.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    /// <summary>
    /// design-decisions.md "Fine-Accrual Job Idempotency" / "Fine-Settlement Consistency": Fine has
    /// no `.Include` needing the two-step lock-then-query pattern - but composing
    /// `FromSqlInterpolated("SELECT *, xmin ...")` directly against Fine breaks anyway, for a
    /// DIFFERENT reason discovered via this repository's own integration tests: <see cref="Fine.Amount"/>'s
    /// <c>ComplexProperty</c> mapping needs EF to reshape the raw "*" projection into its own
    /// <c>amount</c>/<c>currency</c> column names, and that reshaping fails ("column
    /// u.Amount_Amount does not exist" - EF falls back to the ComplexProperty's DEFAULT naming
    /// convention instead of the configured `HasColumnName` calls). The two-step form (raw lock,
    /// discard, then an ordinary tracked LINQ query - the same shape <see cref="GetOpenOverdueFineForUpdateAsync"/>
    /// already uses) sidesteps the bug entirely, since the second query is plain LINQ, not a
    /// FromSql composition.
    /// </summary>
    public async Task<Fine?> GetByIdForUpdateAsync(FineId id, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM library.fines WHERE id = {id.Value} FOR UPDATE", cancellationToken).ConfigureAwait(false);
        return await context.Fines.FirstOrDefaultAsync(f => f.Id == id, cancellationToken).ConfigureAwait(false);
    }

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
