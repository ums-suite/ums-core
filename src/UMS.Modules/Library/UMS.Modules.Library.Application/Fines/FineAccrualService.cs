using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Fines;

namespace UMS.Modules.Library.Application.Fines;

/// <summary>
/// LIB-11: design-decisions.md "Fine-Accrual Job Idempotency" - row-locks the Loan before each daily
/// accrual write, re-checks <c>Status == Active</c> post-lock, plus the (loan_id, accrual_date)
/// unique-constraint backstop - both together, not either alone.
/// </summary>
public sealed class FineAccrualService(
    ILoanRepository loans,
    IFineRepository fines,
    IFineAccrualRepository fineAccruals,
    LibraryOptions options,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<int> AccrueAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var candidates = await loans.GetOverdueActiveAsync(now, batchSize, cancellationToken).ConfigureAwait(false);
        var accruedCount = 0;

        foreach (var candidate in candidates)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var loan = await loans.GetByIdForUpdateAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
            if (loan is null || !loan.IsOverdue(now))
            {
                // A return/renewal committed between the unlocked candidate read and this lock -
                // the exact race edge-cases.md "Fine-accrual job racing a book return" describes.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (await fineAccruals.ExistsForLoanAndDateAsync(loan.Id.Value, today, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var increment = Money.Create(options.FineDailyRateBdt);
            if (increment.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            var openFine = await fines.GetOpenOverdueFineForUpdateAsync(loan.Id.Value, cancellationToken).ConfigureAwait(false);
            if (openFine is null)
            {
                var created = Fine.AccrueNew(loan.Id.Value, loan.BorrowerId, loan.BorrowerType, increment.Value, now);
                if (created.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                fines.Add(created.Value);
                fineAccruals.Add(new Domain.Fines.FineAccrual(Guid.NewGuid(), created.Value.Id.Value, loan.Id.Value, today, increment.Value.Amount, now));
            }
            else
            {
                var increased = openFine.IncreaseAccrual(increment.Value, now);
                if (increased.IsFailure)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                fineAccruals.Add(new Domain.Fines.FineAccrual(Guid.NewGuid(), openFine.Id.Value, loan.Id.Value, today, increment.Value.Amount, now));
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            accruedCount++;
        }

        return accruedCount;
    }
}
