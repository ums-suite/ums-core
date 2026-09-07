using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Ledger;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Application.Invoices;

/// <summary>
/// FIN-2/FIN-3/FIN-12: <c>CreateInvoice</c> plus ownership-scoped reads. requirement-spec.md §2:
/// "Finance never initiates an Invoice on its own trigger" - every call here originates from a
/// caller module (via <c>UMS.Shared.Finance.IInvoiceRequester</c> in-process, or the equivalent
/// <c>POST /api/v1/finance/invoices</c> HTTP surface for a system credential).
/// </summary>
public sealed class InvoiceService(
    IFeeStructureRepository feeStructures,
    IInvoiceRepository invoices,
    ILedgerEntryRepository ledgerEntries,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IClock clock)
{
    public async Task<Result<InvoiceDto>> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.SourceModule) || string.IsNullOrWhiteSpace(request.SourceReferenceId) || string.IsNullOrWhiteSpace(request.FeeType))
        {
            return Error.Validation("invoice.request_fields_required", "sourceModule, sourceReferenceId, and feeType are all required.");
        }

        if (request.OwnerId == Guid.Empty)
        {
            return Error.Validation("invoice.owner_id_required", "ownerId is required.");
        }

        // requirement-spec.md §8 / edge-cases.md "CreateInvoice called twice": checked up front so
        // the common sequential-retry case never even attempts a second insert.
        var existing = await invoices.GetByNaturalKeyAsync(request.SourceModule, request.SourceReferenceId, request.FeeType, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return ToDto(existing);
        }

        var feeStructure = await ResolveFeeStructureAsync(request.FeeType, request.ApplicabilityReferenceId, cancellationToken).ConfigureAwait(false);
        if (feeStructure is null)
        {
            return Error.NotFound("invoice.fee_structure_not_found", $"No Active FeeStructure could be resolved for feeType '{request.FeeType}'.");
        }

        var now = clock.UtcNow;
        var created = Invoice.Create(request.SourceModule, request.SourceReferenceId, request.FeeType, request.OwnerId, feeStructure, now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var invoice = created.Value;
        invoices.Add(invoice);

        // FIN-12/design-decisions.md "Ledger-Entry Atomicity Mechanism": direct, same-transaction
        // write - never routed through the outbox itself.
        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString() : request.CorrelationId;
        var ledgerEntry = LedgerEntry.Create(
            LedgerEntryType.InvoiceRaised,
            "Invoice",
            invoice.Id.Value,
            invoice.TotalAmount.Amount,
            invoice.TotalAmount.Currency,
            $"Invoice raised for {invoice.FeeType} ({invoice.SourceModule}/{invoice.SourceReferenceId}).",
            correlationId,
            now);
        ledgerEntries.Add(ledgerEntry);
        domainEvents.Enqueue(new Domain.Events.LedgerEntryPosted(ledgerEntry.Id.Value, ledgerEntry.EntryType.ToString(), ledgerEntry.ReferenceType, ledgerEntry.ReferenceId, ledgerEntry.Amount, now));

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateValueException)
        {
            // edge-cases.md "Concurrent CreateInvoice Calls for the Same Tuple": a true concurrent
            // double-insert loses the unique-constraint race here - the loser re-fetches and
            // returns the winner's already-committed row, exactly like the sequential-retry path
            // above, rather than surfacing a raw constraint error to a system-to-system caller.
            var winner = await invoices.GetByNaturalKeyAsync(request.SourceModule, request.SourceReferenceId, request.FeeType, cancellationToken).ConfigureAwait(false);
            return winner is null
                ? Error.Failure("invoice.creation_race_unresolved", "A concurrent Invoice creation conflict could not be resolved.")
                : ToDto(winner);
        }

        return ToDto(invoice);
    }

    public async Task<Result<InvoiceDto>> GetByIdAsync(Guid id, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var invoice = await invoices.GetByIdAsync(new InvoiceId(id), cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            return Error.NotFound("invoice.not_found", $"No Invoice exists with id '{id}'.");
        }

        return invoice.OwnerId != callerUserId
            ? Error.Forbidden("invoice.not_owner", "You do not own this Invoice.")
            : ToDto(invoice);
    }

    public async Task<Result<IReadOnlyList<InvoiceDto>>> ListByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var owned = await invoices.GetByOwnerAsync(ownerId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<InvoiceDto> dtos = owned.Select(ToDto).ToList();
        return Result.Success(dtos);
    }

    internal static InvoiceDto ToDto(Invoice invoice) => new(
        invoice.Id.Value,
        invoice.SourceModule,
        invoice.SourceReferenceId,
        invoice.FeeType,
        invoice.OwnerId,
        invoice.TotalAmount.Amount,
        invoice.TotalAmount.Currency,
        invoice.Status.ToString(),
        invoice.CreatedAt,
        invoice.PaidAt);

    private async Task<FeeStructure?> ResolveFeeStructureAsync(string feeType, Guid? applicabilityReferenceId, CancellationToken cancellationToken)
    {
        if (applicabilityReferenceId is Guid referenceId)
        {
            var byProgram = await feeStructures.GetActiveAsync(feeType, FeeApplicabilityType.Program, referenceId, null, cancellationToken).ConfigureAwait(false);
            if (byProgram is not null)
            {
                return byProgram;
            }

            var byCampaign = await feeStructures.GetActiveAsync(feeType, FeeApplicabilityType.AdmissionCampaign, referenceId, null, cancellationToken).ConfigureAwait(false);
            if (byCampaign is not null)
            {
                return byCampaign;
            }
        }

        return await feeStructures.GetActiveAsync(feeType, FeeApplicabilityType.Service, null, feeType, cancellationToken).ConfigureAwait(false);
    }
}
