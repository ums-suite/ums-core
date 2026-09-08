using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>design-decisions.md "Fine-Accrual Job Idempotency" - the (loan_id, accrual_date) unique-constraint backstop's own repository surface.</summary>
public interface IFineAccrualRepository
{
    public Task<bool> ExistsForLoanAndDateAsync(Guid loanId, DateOnly accrualDate, CancellationToken cancellationToken = default);

    public void Add(FineAccrual accrual);
}
