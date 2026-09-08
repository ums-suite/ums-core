using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Loans;
using UMS.Modules.Library.Application.Reservations;
using UMS.Modules.Library.Domain.Catalog;
using UMS.Modules.Library.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Application.Catalog;

/// <summary>
/// LIB-15: <c>GET /digital-resources/{id}/access</c> - requirement-spec.md §2 Digital Resource
/// Access, §9 decision 4. An <see cref="Book.IsOpenAccessDigital"/> Book is served as a direct,
/// always-Available grant with no Loan at all; every other digital resource routes through the
/// EXACT SAME <see cref="LoanService.IssueAsync"/>/<see cref="ReservationService.CreateAsync"/> paths
/// physical circulation uses - design-decisions.md "Digital-resource concurrent-seat exhaustion"
/// explicitly rejects a second, digital-specific concurrency mechanism.
/// </summary>
public sealed class DigitalResourceAccessService(
    IBookRepository books,
    IBookCopyRepository bookCopies,
    LoanService loanService,
    ReservationService reservationService)
{
    public async Task<Result<DigitalResourceAccessDto>> RequestAccessAsync(Guid bookId, Guid borrowerId, BorrowerType borrowerType, string correlationId, CancellationToken cancellationToken = default)
    {
        var typedBookId = new BookId(bookId);
        var book = await books.GetByIdAsync(typedBookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return Error.NotFound("book.not_found", $"No Book exists with id '{bookId}'.");
        }

        if (book.IsOpenAccessDigital)
        {
            return new DigitalResourceAccessDto("Granted", LoanId: null, ReservationId: null);
        }

        var copies = await bookCopies.GetByBookAsync(typedBookId, cancellationToken).ConfigureAwait(false);
        var digitalCandidates = copies.Where(c => c.CopyType == CopyType.Digital && c.Status is BookCopyStatus.Available or BookCopyStatus.Reserved).ToList();
        if (digitalCandidates.Count == 0 && copies.All(c => c.CopyType != CopyType.Digital))
        {
            return Error.Conflict("digital_resource.no_digital_copies", $"Book '{bookId}' has no digital-resource copies configured.");
        }

        // Each candidate is attempted through the ordinary Loan-issuance path, which is itself the
        // sole arbiter of whether a Reserved copy actually belongs to this borrower (LIB-9's own
        // claim check) - a losing attempt here is simply skipped, not surfaced as an error.
        foreach (var candidate in digitalCandidates)
        {
            var issued = await loanService.IssueAsync(candidate.Id.Value, borrowerId, borrowerType, borrowerId, correlationId, cancellationToken).ConfigureAwait(false);
            if (issued.IsSuccess)
            {
                return new DigitalResourceAccessDto("Granted", issued.Value.Id, ReservationId: null);
            }
        }

        // requirement-spec.md §8: "a licensed digital resource's concurrent-seat limit is reached →
        // behaves identically to a fully-loaned physical title: new requests queue as a Reservation."
        var reserved = await reservationService.CreateAsync(bookId, borrowerId, borrowerType, cancellationToken).ConfigureAwait(false);
        return reserved.Match<Result<DigitalResourceAccessDto>>(
            r => new DigitalResourceAccessDto("Queued", LoanId: null, r.Id),
            err => err.Code == "reservation.already_queued" ? new DigitalResourceAccessDto("Queued", LoanId: null, ReservationId: null) : err);
    }
}

public sealed record DigitalResourceAccessDto(string Status, Guid? LoanId, Guid? ReservationId);
