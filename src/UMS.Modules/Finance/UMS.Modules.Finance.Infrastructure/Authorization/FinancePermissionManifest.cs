using UMS.Modules.Finance.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Finance.Infrastructure.Authorization;

/// <summary>Finance's own contribution to the platform-wide Permission catalog (requirement-spec.md §2). Mirrors every other module's own PermissionManifest exactly.</summary>
internal sealed class FinancePermissionManifest : IPermissionManifest
{
    public string OwningModule => "finance";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(FinancePermissions.FeeStructureManage, "Create and publish new versions of a FeeStructure (Admin/Accountant)."),
        new(FinancePermissions.InvoiceCreate, "Raise an Invoice against the shared payment core (system-to-system, caller modules only)."),
        new(FinancePermissions.InvoiceRead, "Read Invoices (Accountant/Admin batch access - an Invoice's own owner reads it via ownership, not this permission)."),
        new(FinancePermissions.PaymentInitiate, "Initiate a Payment against an owned Invoice."),
        new(FinancePermissions.PaymentRead, "Read Payments (Accountant/Admin batch access - a Payment's own owner reads it via ownership, not this permission)."),
        new(FinancePermissions.PaymentRefund, "Refund a Payment (Accountant/Admin) - reserved for the remainder Finance pass, release/DEVELOPMENT_PLAN.md Flow #18; no endpoint gates on it yet."),
        new(FinancePermissions.LedgerRead, "Read LedgerEntry rows (Accountant/Reporting) - reserved for the remainder Finance pass, Flow #18; no endpoint gates on it yet."),
        new(FinancePermissions.ReconciliationReview, "Review a ReconciliationException (Accountant) - reserved for the remainder Finance pass, Flow #18; no endpoint gates on it yet."),
    ];
}
