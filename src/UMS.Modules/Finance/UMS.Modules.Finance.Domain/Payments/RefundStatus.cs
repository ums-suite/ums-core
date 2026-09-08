namespace UMS.Modules.Finance.Domain.Payments;

/// <summary>
/// FIN-11: a <see cref="Refund"/> is resolved synchronously against the (fake) gateway or by an
/// Accountant's manual settlement in this build - there is no separate pending/in-flight state
/// (unlike <see cref="PaymentStatus"/>'s own async webhook-driven machine) since neither path in
/// requirement-spec.md §9's refund-execution decision describes an asynchronous confirmation step
/// for this pass.
/// </summary>
public enum RefundStatus
{
    Succeeded,
    Failed,
}
