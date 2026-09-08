using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Reservations;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Application.Reservations;

/// <summary>LIB-4: requirement-spec.md §2 Reservation Queueing.</summary>
public sealed class ReservationService(
    IBookRepository books,
    IBookCopyRepository bookCopies,
    IReservationRepository reservations,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>
    /// requirement-spec.md §4 invariant: "A Reservation may only be created when zero BookCopy rows
    /// for the Book are currently Available - validated transactionally at creation time." Takes a
    /// row lock on every BookCopy of this Book first (the same lock family every other Copy-state
    /// writer takes), so this check is genuinely serialized against a concurrent return/issuance, not
    /// a plain unlocked read.
    /// </summary>
    public async Task<Result<ReservationDto>> CreateAsync(Guid bookId, Guid borrowerId, BorrowerType borrowerType, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var typedBookId = new BookId(bookId);
        var book = await books.GetByIdAsync(typedBookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("book.not_found", $"No Book exists with id '{bookId}'.");
        }

        if (book.Withdrawn)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("book.withdrawn", $"Book '{bookId}' has been withdrawn from the catalog and cannot accept new Reservations.");
        }

        if (await reservations.HasOpenReservationAsync(bookId, borrowerId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("reservation.already_queued", $"Borrower '{borrowerId}' already holds an open Reservation for Book '{bookId}'.");
        }

        await bookCopies.LockAllByBookAsync(typedBookId, cancellationToken).ConfigureAwait(false);
        var availableCount = await bookCopies.CountAvailableByBookAsync(typedBookId, cancellationToken).ConfigureAwait(false);
        if (availableCount > 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("reservation.copy_available", $"Book '{bookId}' currently has an available copy - issue a Loan directly instead of reserving.");
        }

        var created = Reservation.Create(bookId, borrowerId, borrowerType, clock.UtcNow);
        if (created.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return created.Error!;
        }

        reservations.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<IReadOnlyList<ReservationDto>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        (await reservations.GetByBorrowerAsync(borrowerId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    internal static ReservationDto ToDto(Reservation reservation) => new(
        reservation.Id.Value,
        reservation.BookId,
        reservation.BorrowerId,
        reservation.BorrowerType.ToString(),
        reservation.Priority,
        reservation.Status.ToString(),
        reservation.OfferedCopyId,
        reservation.OfferedAt,
        reservation.ClaimWindowExpiresAt,
        reservation.ClaimedAt,
        reservation.ExpiredAt,
        reservation.CreatedAt);
}
