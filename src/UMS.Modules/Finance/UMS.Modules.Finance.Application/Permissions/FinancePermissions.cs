namespace UMS.Modules.Finance.Application.Permissions;

/// <summary>
/// requirement-spec.md finance §2 Permission Strings (ADR-0006). <c>PaymentRefund</c> gates
/// <c>POST /payments/{id}/refund</c> and <c>LedgerRead</c> gates <c>GET /ledger-entries</c> (both
/// FIN-11/FIN-13, Flow #18's remainder Finance pass). <c>ReconciliationReview</c> is still declared
/// only for the full spec's catalog - FIN-14's daily reconciliation job (Flow #18) writes
/// <c>ReconciliationException</c> rows for manual review, but the Accountant-facing review/resolution
/// read surface itself is a further, not-yet-decomposed ticket.
/// </summary>
public static class FinancePermissions
{
    public const string FeeStructureManage = "finance.feestructure.manage";
    public const string InvoiceCreate = "finance.invoice.create";
    public const string InvoiceRead = "finance.invoice.read";
    public const string PaymentInitiate = "finance.payment.initiate";
    public const string PaymentRead = "finance.payment.read";
    public const string PaymentRefund = "finance.payment.refund";
    public const string LedgerRead = "finance.ledger.read";
    public const string ReconciliationReview = "finance.reconciliation.review";
}
