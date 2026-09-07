using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Invoices;

/// <summary>
/// requirement-spec.md finance §3: "Contains InvoiceItems (value objects)". A snapshot, not a live
/// reference - <see cref="FeeStructureVersion"/> and the amount are captured once, at
/// Invoice-creation time, from the <c>FeeStructure</c> that was Active at that instant, so a later
/// change to that FeeStructure can never retroactively alter this already-generated Invoice
/// (requirement-spec.md §2).
///
/// <para>
/// <see cref="Amount"/>/<see cref="Currency"/> are plain scalars rather than a nested
/// <see cref="Money"/> deliberately - EF Core's owned-collection mapping (this type lives inside
/// <c>Invoice.Items</c>, an <c>OwnsMany</c> collection) has no <c>ComplexProperty</c> support one
/// level inside an already-owned entity in this EF Core version (only at a top-level
/// <c>EntityTypeBuilder&lt;T&gt;</c>, as <c>Invoice.TotalAmount</c>'s own mapping uses), and
/// <c>Money</c> being a struct rules out the <c>OwnsOne</c> alternative too (<c>OwnsOne</c>/
/// <c>OwnsMany</c> require a reference type). <see cref="AsMoney"/> reconstructs the value object
/// for any caller that wants it.
/// </para>
/// </summary>
public sealed record InvoiceItem(Guid FeeStructureId, int FeeStructureVersion, string Description, decimal Amount, string Currency)
{
    public Money AsMoney => Money.FromStoredValue(Amount, Currency);
}
