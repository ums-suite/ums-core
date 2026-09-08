using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Events;
using UMS.Modules.Library.Domain.Loans;

namespace UMS.Modules.Library.Application.Loans;

/// <summary>
/// LIB-10: requirement-spec.md §3 - "a scheduled/detected transition (computed, not a distinct
/// persisted status)"; §4 invariant "Overdue is a computed state ... never a separately-written
/// status column - this avoids a background job racing a concurrent return/renewal write." This job
/// applies that SAME defensive posture to its own write path (the <see cref="OverdueNotice"/> marker
/// insert + <see cref="LoanOverdue"/> event), mirroring design-decisions.md "Fine-Accrual Job
/// Idempotency"'s row-lock-plus-unique-constraint pattern exactly, just applied to detection instead
/// of money.
/// </summary>
public sealed class LoanOverdueDetectionService(
    ILoanRepository loans,
    IOverdueNoticeRepository overdueNotices,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEventRecorder,
    IClock clock)
{
    public async Task<int> DetectAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var candidates = await loans.GetOverdueActiveAsync(now, batchSize, cancellationToken).ConfigureAwait(false);
        var notifiedCount = 0;

        foreach (var candidate in candidates)
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var locked = await loans.GetByIdForUpdateAsync(candidate.Id, cancellationToken).ConfigureAwait(false);
            if (locked is null || !locked.IsOverdue(now))
            {
                // A concurrent return/renewal committed between the unlocked candidate read above and
                // this lock acquisition - not an error, just a no-op (the same race the row lock
                // exists to catch, per requirement-spec.md §4's own rationale).
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (await overdueNotices.ExistsForLoanAndDateAsync(locked.Id.Value, today, cancellationToken).ConfigureAwait(false))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            overdueNotices.Add(new OverdueNotice(Guid.NewGuid(), locked.Id.Value, today, now));
            domainEventRecorder.Enqueue(new LoanOverdue(locked.Id.Value, locked.BookCopyId, locked.BorrowerId, locked.DueDate, now));

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            notifiedCount++;
        }

        return notifiedCount;
    }
}
