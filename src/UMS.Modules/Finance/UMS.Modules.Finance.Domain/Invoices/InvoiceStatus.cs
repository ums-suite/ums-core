namespace UMS.Modules.Finance.Domain.Invoices;

/// <summary>requirement-spec.md finance §4: "Invoice is satisfied by exactly one Successful/Reconciled Payment covering its full amount" - <see cref="Open"/> is the only state that still accepts a new Payment initiation.</summary>
public enum InvoiceStatus
{
    Open,
    Paid,
    Voided,
}
