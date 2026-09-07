namespace UMS.Modules.Finance.Domain.Ledger;

/// <summary>requirement-spec.md finance §2: "one row per financial event (invoice raised, payment posted, refund posted, reconciliation correction)". <see cref="RefundPosted"/>/<see cref="ReconciliationCorrection"/> are modeled here for schema completeness against the full spec but are not produced by this build's Payment Core slice (release/DEVELOPMENT_PLAN.md Flow #14) - Refund and daily reconciliation are the remainder Finance pass, Flow #18.</summary>
public enum LedgerEntryType
{
    InvoiceRaised,
    PaymentPosted,
    RefundPosted,
    ReconciliationCorrection,
}
