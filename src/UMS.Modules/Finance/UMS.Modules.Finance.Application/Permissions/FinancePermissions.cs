namespace UMS.Modules.Finance.Application.Permissions;

/// <summary>requirement-spec.md finance §2 Permission Strings (ADR-0006). <c>Refund</c>/<c>LedgerRead</c>/<c>ReconciliationReview</c> are declared here per the full spec's catalog but have no gated endpoint yet in this build's Payment Core slice (release/DEVELOPMENT_PLAN.md Flow #14) - Refund workflow, ledger reporting, and reconciliation review are the remainder Finance pass, Flow #18.</summary>
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
