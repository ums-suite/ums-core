using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class FineAccrualRepository(LibraryDbContext context) : IFineAccrualRepository
{
    public Task<bool> ExistsForLoanAndDateAsync(Guid loanId, DateOnly accrualDate, CancellationToken cancellationToken = default) =>
        context.FineAccruals.AnyAsync(a => a.LoanId == loanId && a.AccrualDate == accrualDate, cancellationToken);

    public void Add(FineAccrual accrual) => context.FineAccruals.Add(accrual);
}
