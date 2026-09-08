using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class LoanReviewFlagRepository(LibraryDbContext context) : ILoanReviewFlagRepository
{
    public async Task<IReadOnlyList<LoanReviewFlag>> GetByLoanAsync(Guid loanId, CancellationToken cancellationToken = default) =>
        await context.LoanReviewFlags.Where(f => f.LoanId == loanId).OrderByDescending(f => f.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(LoanReviewFlag flag) => context.LoanReviewFlags.Add(flag);
}
