using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Application.Abstractions;

public interface ILoanReviewFlagRepository
{
    public Task<IReadOnlyList<LoanReviewFlag>> GetByLoanAsync(Guid loanId, CancellationToken cancellationToken = default);

    public void Add(LoanReviewFlag flag);
}
