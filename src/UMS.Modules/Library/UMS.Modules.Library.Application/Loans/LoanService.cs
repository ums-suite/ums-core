using System.Text.Json;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Domain.Catalog;
using UMS.Modules.Library.Domain.Common;
using UMS.Modules.Library.Domain.Loans;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Application.Loans;

/// <summary>
/// LIB-5..8: the copy-issuance-concurrency-safe command (the pivot every downstream reservation/fine
/// ticket depends on transitively), renewal, return, and the borrower's own loan-history read.
/// </summary>
public sealed class LoanService(
    IBookRepository books,
    ICategoryRepository categories,
    IBookCopyRepository bookCopies,
    ILoanRepository loans,
    IReservationRepository reservations,
    IFineRepository fines,
    BorrowerContextService borrowerContext,
    LibraryOptions options,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>
    /// LIB-5: design-decisions.md "Copy-Issuance Concurrency Control Pattern" - locks the target
    /// BookCopy row first, then checks EVERY borrower-eligibility gate (active status, max-concurrent-
    /// loan cap, unpaid-fine block) inside the SAME transaction (design-decisions.md
    /// "Fine-Blocks-New-Loan Enforcement Point": "one atomic gate for all borrower-eligibility
    /// conditions, not a separate pre-flight step"). Also serves LIB-9's own claim path and LIB-15's
    /// digital-resource access grant - both are, mechanically, just this same call against a
    /// BookCopy that happens to already be <see cref="BookCopyStatus.Reserved"/> for this borrower.
    /// </summary>
    public async Task<Result<LoanDto>> IssueAsync(Guid bookCopyId, Guid borrowerId, BorrowerType borrowerType, Guid issuedByUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var copy = await bookCopies.GetByIdForUpdateAsync(new BookCopyId(bookCopyId), cancellationToken).ConfigureAwait(false);
        if (copy is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("book_copy.not_found", $"No BookCopy exists with id '{bookCopyId}'.");
        }

        var book = await books.GetByIdAsync(copy.BookId, cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("book.not_found", $"No Book exists with id '{copy.BookId}'.");
        }

        if (book.Withdrawn)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("book.withdrawn", $"Book '{book.Id}' has been withdrawn from the catalog and is no longer loan-eligible.");
        }

        if (book.CategoryId is { } categoryId)
        {
            var category = await categories.GetByIdAsync(categoryId, cancellationToken).ConfigureAwait(false);
            if (category is { IsReferenceOnly: true })
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error.Conflict("book.reference_only", $"Book '{book.Id}' belongs to a reference-only Category and is not loan-eligible.");
            }
        }

        // A copy already Reserved is only loan-eligible to the specific borrower it was offered to,
        // within their bounded claim window (LIB-9's own residual note: "claiming a reservation offer
        // is, mechanically, just issuing a Loan"). A copy already OnLoan/Lost/Withdrawn is never
        // loan-eligible to anyone.
        Domain.Reservations.Reservation? claimedReservation = null;
        if (copy.Status == BookCopyStatus.Reserved)
        {
            var offered = await reservations.GetOfferedByCopyForUpdateAsync(bookCopyId, cancellationToken).ConfigureAwait(false);
            if (offered is null || offered.BorrowerId != borrowerId)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error.Conflict("book_copy.reserved_for_other_borrower", $"BookCopy '{bookCopyId}' is currently held for another borrower's Reservation.");
            }

            claimedReservation = offered;
        }
        else if (copy.Status != BookCopyStatus.Available)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("book_copy.not_available", $"BookCopy '{bookCopyId}' cannot be loaned out - it is currently '{copy.Status}'.");
        }

        var now = clock.UtcNow;

        var status = await borrowerContext.GetCurrentStatusAsync(borrowerId, borrowerType, cancellationToken).ConfigureAwait(false);
        if (status.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return status.Error!;
        }

        if (!string.Equals(status.Value, "Active", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Forbidden("library.borrower_not_active", $"Borrower '{borrowerId}' is not Active (currently '{status.Value}') and may not take a new Loan.");
        }

        var maxConcurrentLoans = borrowerType == BorrowerType.Student ? options.MaxConcurrentLoansStudent : options.MaxConcurrentLoansFaculty;
        var activeLoanCount = await loans.CountActiveByBorrowerAsync(borrowerId, cancellationToken).ConfigureAwait(false);
        if (activeLoanCount >= maxConcurrentLoans)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("library.max_concurrent_loans_exceeded", $"Borrower '{borrowerId}' already holds the maximum of {maxConcurrentLoans} concurrent Loan(s).");
        }

        // requirement-spec.md §8: "a borrower with an unpaid Fine attempts a new loan → rejected with
        // a machine-readable reason pointing to the outstanding fine, not a generic denial."
        if (await fines.HasUnsettledFineAsync(borrowerId, cancellationToken).ConfigureAwait(false))
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("library.unpaid_fine_blocks_loan", $"Borrower '{borrowerId}' has an outstanding, unsettled Fine and may not take a new Loan until it is paid or waived.");
        }

        var loanPeriodDays = borrowerType == BorrowerType.Student ? options.LoanPeriodDaysStudent : options.LoanPeriodDaysFaculty;
        var dueDate = now.AddDays(loanPeriodDays);

        var marked = copy.MarkOnLoan();
        if (marked.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return marked.Error!;
        }

        if (claimedReservation is not null)
        {
            var claimed = claimedReservation.Claim(now);
            if (claimed.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return claimed.Error!;
            }
        }

        var created = Loan.Issue(bookCopyId, book.Id.Value, borrowerId, borrowerType, issuedByUserId, dueDate, now);
        if (created.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return created.Error!;
        }

        var loan = created.Value;
        loans.Add(loan);

        var afterJson = JsonSerializer.Serialize(new { bookCopyId, borrowerId, borrowerType = borrowerType.ToString(), dueDate });
        var audit = new AuditContext(issuedByUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Loan", loan.Id.Value.ToString(), AuditActions.Create, beforeValueJson: null, afterJson);

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(loan);
    }

    /// <summary>LIB-7: requirement-spec.md §4 "Renewal is rejected if an active Reservation queue is non-empty" - the same row-lock family as issuance (design-decisions.md "Concurrent Return/Renewal Conflict Contract").</summary>
    public async Task<Result<LoanDto>> RenewAsync(Guid loanId, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var loan = await loans.GetByIdForUpdateAsync(new LoanId(loanId), cancellationToken).ConfigureAwait(false);
        if (loan is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("loan.not_found", $"No Loan exists with id '{loanId}'.");
        }

        if (loan.Status != Domain.Loans.LoanStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict(
                loan.Status == Domain.Loans.LoanStatus.Returned ? "loan.already_returned" : "loan.already_lost_write_off",
                $"Loan '{loanId}' cannot be renewed - it is currently '{loan.Status}'.");
        }

        var queueDepth = await reservations.CountOpenByBookAsync(loan.BookId, cancellationToken).ConfigureAwait(false);
        if (queueDepth > 0)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("loan.reservation_queue_blocks_renewal", $"Book '{loan.BookId}' has {queueDepth} waiting Reservation(s) - this Loan cannot be renewed.");
        }

        if (loan.RenewalCount >= options.MaxRenewalCount)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("loan.max_renewals_exceeded", $"Loan '{loanId}' has already reached the maximum of {options.MaxRenewalCount} renewal(s).");
        }

        var now = clock.UtcNow;
        var loanPeriodDays = loan.BorrowerType == BorrowerType.Student ? options.LoanPeriodDaysStudent : options.LoanPeriodDaysFaculty;
        var newDueDate = now.AddDays(loanPeriodDays);

        var renewed = loan.Renew(newDueDate, now);
        if (renewed.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return renewed.Error!;
        }

        var audit = new AuditContext(actorUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Loan", loan.Id.Value.ToString(), AuditActions.Update, beforeValueJson: null, JsonSerializer.Serialize(new { newDueDate, renewalCount = loan.RenewalCount }));

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(loan);
    }

    /// <summary>LIB-8: takes the SAME Loan-row lock as <see cref="RenewAsync"/>, plus the target BookCopy's own lock (symmetric with issuance) to flip it back to Available.</summary>
    public async Task<Result<LoanDto>> ReturnAsync(Guid loanId, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var loan = await loans.GetByIdForUpdateAsync(new LoanId(loanId), cancellationToken).ConfigureAwait(false);
        if (loan is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("loan.not_found", $"No Loan exists with id '{loanId}'.");
        }

        if (loan.Status != Domain.Loans.LoanStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict(
                loan.Status == Domain.Loans.LoanStatus.Returned ? "loan.already_returned" : "loan.already_lost_write_off",
                $"Loan '{loanId}' cannot be returned - it is currently '{loan.Status}'.");
        }

        var copy = await bookCopies.GetByIdForUpdateAsync(new BookCopyId(loan.BookCopyId), cancellationToken).ConfigureAwait(false);
        if (copy is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("book_copy.not_found", $"No BookCopy exists with id '{loan.BookCopyId}'.");
        }

        var now = clock.UtcNow;
        var returned = loan.Return(now);
        if (returned.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return returned.Error!;
        }

        var freed = copy.MarkAvailable();
        if (freed.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return freed.Error!;
        }

        var audit = new AuditContext(actorUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Loan", loan.Id.Value.ToString(), AuditActions.Update, beforeValueJson: null, JsonSerializer.Serialize(new { status = loan.Status.ToString(), returnedAt = loan.ReturnedAt }));

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(loan);
    }

    public async Task<Result<LoanDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var loan = await loans.GetByIdAsync(new LoanId(id), cancellationToken).ConfigureAwait(false);
        return loan is null ? Error.NotFound("loan.not_found", $"No Loan exists with id '{id}'.") : ToDto(loan);
    }

    public async Task<IReadOnlyList<LoanDto>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        (await loans.GetByBorrowerAsync(borrowerId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    internal LoanDto ToDto(Domain.Loans.Loan loan) => new(
        loan.Id.Value,
        loan.BookCopyId,
        loan.BookId,
        loan.BorrowerId,
        loan.BorrowerType.ToString(),
        loan.IssuedByUserId,
        loan.IssuedAt,
        loan.DueDate,
        loan.RenewalCount,
        loan.Status.ToString(),
        loan.IsOverdue(clock.UtcNow),
        loan.ReturnedAt,
        loan.LostWriteOffAt);
}
