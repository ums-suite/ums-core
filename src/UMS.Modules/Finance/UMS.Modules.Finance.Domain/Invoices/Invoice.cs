using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Domain.Invoices;

/// <summary>
/// FIN-2: raised by <c>CreateInvoice</c>, called only by Admission/Student/Hostel/Library/Alumni
/// (requirement-spec.md finance §2: "Finance never initiates an Invoice on its own trigger").
///
/// <para>
/// <b>Dedup key.</b> <c>(SourceModule, SourceReferenceId, FeeType)</c> carries its own DB-level
/// uniqueness constraint (InvoiceConfiguration) - a caller's own retry, or a genuinely concurrent
/// double-call (edge-cases.md "Concurrent CreateInvoice Calls for the Same Tuple"), is always
/// resolved by catching that violation and re-fetching the already-committed row
/// (<c>InvoiceService.CreateAsync</c>), never by a raw constraint error reaching the caller.
/// </para>
/// </summary>
public sealed class Invoice : AggregateRoot<InvoiceId>
{
    private readonly List<InvoiceItem> _items = [];

    private Invoice()
    {
    }

    private Invoice(InvoiceId id, string sourceModule, string sourceReferenceId, string feeType, Guid ownerId, IReadOnlyCollection<InvoiceItem> items, Money totalAmount, DateTimeOffset now)
    {
        Id = id;
        SourceModule = sourceModule;
        SourceReferenceId = sourceReferenceId;
        FeeType = feeType;
        OwnerId = ownerId;
        _items.AddRange(items);
        TotalAmount = totalAmount;
        Status = InvoiceStatus.Open;
        CreatedAt = now;
    }

    public string SourceModule { get; private set; } = string.Empty;

    public string SourceReferenceId { get; private set; } = string.Empty;

    public string FeeType { get; private set; } = string.Empty;

    public Guid OwnerId { get; private set; }

    public IReadOnlyCollection<InvoiceItem> Items => _items.AsReadOnly();

    public Money TotalAmount { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    /// <summary>requirement-spec.md §4: "Invoice refusing a second Payment once its outstanding balance reaches zero" - the read this check performs must happen under the pessimistic Invoice row lock design-decisions.md's "Invoice-Level Concurrency Control for Payment Initiation" specifies, not on a plain unlocked read.</summary>
    public bool HasOutstandingBalance => Status == InvoiceStatus.Open;

    public static Result<Invoice> Create(string sourceModule, string sourceReferenceId, string feeType, Guid ownerId, FeeStructure feeStructure, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(feeStructure);

        if (string.IsNullOrWhiteSpace(sourceModule))
        {
            return Error.Validation("invoice.source_module_required", "An Invoice's sourceModule is required.");
        }

        if (string.IsNullOrWhiteSpace(sourceReferenceId))
        {
            return Error.Validation("invoice.source_reference_id_required", "An Invoice's sourceReferenceId is required.");
        }

        if (ownerId == Guid.Empty)
        {
            return Error.Validation("invoice.owner_id_required", "An Invoice's ownerId is required.");
        }

        if (!feeStructure.IsEffectiveAt(now))
        {
            return Error.Conflict("invoice.fee_structure_not_effective", $"FeeStructure '{feeStructure.Id}' is not currently effective.");
        }

        var item = new InvoiceItem(feeStructure.Id.Value, feeStructure.VersionNumber, $"{feeStructure.FeeType} fee", feeStructure.Amount.Amount, feeStructure.Amount.Currency);
        var invoice = new Invoice(InvoiceId.New(), sourceModule.Trim(), sourceReferenceId.Trim(), feeType.Trim(), ownerId, [item], feeStructure.Amount, now);
        invoice.Raise(new InvoiceGenerated(invoice.Id.Value, invoice.SourceModule, invoice.SourceReferenceId, invoice.FeeType, ownerId, invoice.TotalAmount.Amount, now));
        return invoice;
    }

    /// <summary>Called once the Payment that fully satisfies this Invoice is confirmed Successful (requirement-spec.md §4: "one Payment fully satisfies one Invoice" - no partial/installment tracking in this pass, §9).</summary>
    public Result MarkPaid(DateTimeOffset now)
    {
        if (Status != InvoiceStatus.Open)
        {
            return Result.Failure(Error.Conflict("invoice.invalid_transition", $"Cannot mark Invoice '{Id}' Paid - it is currently '{Status}'."));
        }

        Status = InvoiceStatus.Paid;
        PaidAt = now;
        return Result.Success();
    }
}
