namespace UMS.Modules.Finance.Domain.Payments;

/// <summary>
/// requirement-spec.md finance §2/§4: <c>Initiated -&gt; Pending -&gt; (Successful | Failed) -&gt;
/// Reconciled</c>, forward-only except the explicit reconciliation path.
/// <see cref="PaymentTransaction.TryApplyGatewayStatus"/> is the one place the monotonic ordering
/// guard design-decisions.md's "Webhook Signature Verification and State-Transition Ordering" and
/// edge-cases.md's "Out-of-Order Webhook Delivery" both specify is enforced, via
/// <see cref="PaymentTransaction.Precedence"/> - deliberately NOT this enum's own underlying integer
/// values, since <see cref="Successful"/> and <see cref="Failed"/> are mutually-exclusive
/// alternatives at the SAME precedence tier (either one blocks the other from later overwriting it)
/// but must remain two distinct, independently-comparable enum members.
/// </summary>
public enum PaymentStatus
{
    Initiated,
    Pending,
    Successful,
    Failed,
    Reconciled,
}
