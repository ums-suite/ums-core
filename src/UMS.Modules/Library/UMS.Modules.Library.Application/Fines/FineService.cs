using System.Text.Json;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Common;
using UMS.Modules.Library.Domain.Fines;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Finance;

namespace UMS.Modules.Library.Application.Fines;

/// <summary>LIB-12..14: the borrower's own fine read, settlement-initiation (Finance's CreateInvoice), and librarian waiver.</summary>
public sealed class FineService(
    IFineRepository fines,
    BorrowerContextService borrowerContext,
    IInvoiceRequester invoiceRequester,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<IReadOnlyList<FineDto>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        (await fines.GetByBorrowerAsync(borrowerId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<Result<FineDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fine = await fines.GetByIdAsync(new FineId(id), cancellationToken).ConfigureAwait(false);
        return fine is null ? Error.NotFound("fine.not_found", $"No Fine exists with id '{id}'.") : ToDto(fine);
    }

    /// <summary>
    /// LIB-13: design-decisions.md "Fine-Settlement Consistency - Money-Criticality Tier" - a direct,
    /// transaction-coupled in-process command call to Finance (ADR-0003), never an event.
    /// </summary>
    public async Task<Result<FineDto>> SettleAsync(Guid fineId, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var fine = await fines.GetByIdForUpdateAsync(new FineId(fineId), cancellationToken).ConfigureAwait(false);
        if (fine is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("fine.not_found", $"No Fine exists with id '{fineId}'.");
        }

        if (fine.Status != FineStatus.Accruing)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("fine.not_accruing", $"Fine '{fineId}' cannot be settled - it is currently '{fine.Status}'.");
        }

        var ownerUserId = await borrowerContext.ResolveIdentityUserIdAsync(fine.BorrowerId, fine.BorrowerType, cancellationToken).ConfigureAwait(false);
        if (ownerUserId is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Failure("library.borrower_identity_unresolved", $"Borrower '{fine.BorrowerId}' has no linked Identity user - cannot raise a LibraryFine Invoice.");
        }

        // "LibraryFine" is a bare literal string (not a shared enum) - Finance resolves it against a
        // FeeStructure named exactly that when ApplicabilityReferenceId is null (mirrors Hostel's own
        // "HostelFee" call site exactly).
        var invoice = await invoiceRequester.CreateInvoiceAsync(
            new CreateInvoiceCommand("library", fineId.ToString(), "LibraryFine", ownerUserId.Value, ApplicabilityReferenceId: null, actorUserId, correlationId),
            cancellationToken).ConfigureAwait(false);
        if (invoice.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return invoice.Error!;
        }

        var now = clock.UtcNow;
        var initiated = fine.InitiateSettlement(invoice.Value.InvoiceId, now);
        if (initiated.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return initiated.Error!;
        }

        var audit = new AuditContext(actorUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Fine", fine.Id.Value.ToString(), AuditActions.Update, beforeValueJson: null, JsonSerializer.Serialize(new { status = fine.Status.ToString(), invoiceId = fine.InvoiceId }));

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(fine);
    }

    /// <summary>LIB-14: requirement-spec.md §4 "A Fine waiver requires an audited actor + reason (Money-criticality tier)."</summary>
    public async Task<Result<FineDto>> WaiveAsync(Guid fineId, Guid waivedByUserId, string reason, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var fine = await fines.GetByIdForUpdateAsync(new FineId(fineId), cancellationToken).ConfigureAwait(false);
        if (fine is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("fine.not_found", $"No Fine exists with id '{fineId}'.");
        }

        var waived = fine.Waive(waivedByUserId, reason, clock.UtcNow);
        if (waived.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return waived.Error!;
        }

        var audit = new AuditContext(waivedByUserId, ActorIpAddress: null, correlationId)
            .ToRequest("Fine", fine.Id.Value.ToString(), AuditActions.Update, beforeValueJson: null, JsonSerializer.Serialize(new { status = fine.Status.ToString() }), reason: reason);

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(fine);
    }

    internal static FineDto ToDto(Fine fine) => new(
        fine.Id.Value,
        fine.LoanId,
        fine.BorrowerId,
        fine.BorrowerType.ToString(),
        fine.Reason.ToString(),
        fine.Amount.Amount,
        fine.Amount.Currency,
        fine.Status.ToString(),
        fine.InvoiceId,
        fine.WaivedByUserId,
        fine.WaivedReason,
        fine.CreatedAt,
        fine.SettledAt,
        fine.WaivedAt);
}
