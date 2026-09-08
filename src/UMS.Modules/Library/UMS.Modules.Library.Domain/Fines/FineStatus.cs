namespace UMS.Modules.Library.Domain.Fines;

/// <summary>requirement-spec.md §2/§4: accrues, then settlement is initiated (Finance's CreateInvoice), then Paid on PaymentCompleted - or Waived by a librarian at any point before Paid.</summary>
public enum FineStatus
{
    Accruing,
    PendingSettlement,
    Paid,
    Waived,
}
