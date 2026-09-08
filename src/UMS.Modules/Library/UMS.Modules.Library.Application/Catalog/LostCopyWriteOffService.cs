using System.Text.Json;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Domain.Catalog;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Fines;
using UMS.Modules.Library.Domain.Reservations;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Application.Catalog;

/// <summary>
/// LIB-17: requirement-spec.md §8 "A BookCopy is reported lost mid-loan → the Loan is force-closed,
/// the copy is marked Lost, and a replacement-cost Fine is generated." §5 NFR Auditability:
/// "lost-copy write-offs are 100%-audited sensitive mutations." design-decisions.md "Lost-Copy
/// Write-Off vs. Active Reservation Queue": additionally surfaces a librarian-facing flag (never an
/// automated queue action) when the Book's post-loss supply can no longer realistically satisfy its
/// reservation queue.
/// </summary>
public sealed class LostCopyWriteOffService(
    IBookCopyRepository bookCopies,
    ILoanRepository loans,
    IFineRepository fines,
    IReservationRepository reservations,
    IBookReservationSupplyFlagRepository supplyFlags,
    LibraryOptions options,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result> ReportLostAsync(Guid bookCopyId, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var copy = await bookCopies.GetByIdForUpdateAsync(new BookCopyId(bookCopyId), cancellationToken).ConfigureAwait(false);
        if (copy is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(Error.NotFound("book_copy.not_found", $"No BookCopy exists with id '{bookCopyId}'."));
        }

        var now = clock.UtcNow;
        var activeLoan = await loans.GetActiveByBookCopyForUpdateAsync(bookCopyId, cancellationToken).ConfigureAwait(false);

        if (activeLoan is not null)
        {
            var closed = activeLoan.ForceCloseAsLost(now);
            if (closed.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure(closed.Error!);
            }

            var replacementCost = Money.Create(options.DefaultReplacementCostBdt);
            if (replacementCost.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure(replacementCost.Error!);
            }

            var fine = Fine.CreateReplacementCharge(activeLoan.Id.Value, activeLoan.BorrowerId, activeLoan.BorrowerType, replacementCost.Value, now);
            if (fine.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Result.Failure(fine.Error!);
            }

            fines.Add(fine.Value);
        }

        var lost = copy.MarkLost(now);
        if (lost.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(lost.Error!);
        }

        // design-decisions.md "Lost-Copy Write-Off vs. Active Reservation Queue": compute the Book's
        // post-loss supply-vs-demand and surface a librarian-facing flag - no automated action
        // against any individual Reservation.
        var remainingCopyCount = await bookCopies.CountCirculatingByBookAsync(copy.BookId, cancellationToken).ConfigureAwait(false);
        var queueDepth = await reservations.CountOpenByBookAsync(copy.BookId.Value, cancellationToken).ConfigureAwait(false);
        if (queueDepth > remainingCopyCount)
        {
            var flag = BookReservationSupplyFlag.Create(
                copy.BookId.Value,
                remainingCopyCount,
                queueDepth,
                $"BookCopy '{bookCopyId}' was reported lost - the Book's reservation queue ({queueDepth}) now exceeds its remaining circulating copy count ({remainingCopyCount}).",
                $"BookCopyReportedLost:{bookCopyId}",
                now);
            if (flag.IsSuccess)
            {
                supplyFlags.Add(flag.Value);
            }
        }

        var audit = new AuditContext(actorUserId, ActorIpAddress: null, correlationId).ToRequest(
            "BookCopy",
            bookCopyId.ToString(),
            "lost_write_off",
            beforeValueJson: null,
            JsonSerializer.Serialize(new { status = copy.Status.ToString(), loanId = activeLoan?.Id.Value }));

        return await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
    }
}
