namespace UMS.Modules.Finance.Domain.Payments;

/// <summary>
/// requirement-spec.md finance §3: "one row per gateway-facing attempt/state transition"; the
/// spec's own table describes this as a 1:N child of <see cref="Payment"/>. This build's Payment
/// Core slice (release/DEVELOPMENT_PLAN.md Flow #14) only ever creates exactly ONE
/// PaymentTransaction per Payment - design-decisions.md's "Idempotency-Key + Gateway-Transaction-ID
/// Enforcement Mechanism" explicitly resolves that a genuinely abandoned attempt starts a NEW
/// Payment (with a new IdempotencyKey), not a second attempt under the same Payment. The 1:N shape
/// is still modeled here (a real, independently-queryable child row, not folded into Payment
/// itself) so a later gateway that genuinely needs multiple round trips for one logical Payment (a
/// documented possibility, not yet a real requirement) has schema room without a breaking change -
/// documented as a simplification, not silently narrowed.
///
/// <para>
/// <see cref="TryApplyGatewayStatus"/> is the SOLE mutation path onto <see cref="Status"/> once a
/// gateway status is known - both the live webhook handler and the stuck-payment polling job call
/// it as their only way to move this row forward (design-decisions.md's "Webhook Signature
/// Verification and State-Transition Ordering", explicitly collapsing what would otherwise be two
/// independent triggers into one converging code path - edge-cases.md's "Stuck-Pending Polling Job
/// Racing a Webhook That Arrives Mid-Poll").
/// </para>
/// </summary>
public sealed class PaymentTransaction
{
    /// <summary>The monotonic precedence tier design-decisions.md's ordering guard checks - deliberately NOT <see cref="PaymentStatus"/>'s own underlying enum values (see that enum's remarks): <see cref="PaymentStatus.Successful"/> and <see cref="PaymentStatus.Failed"/> share tier 2 - either one blocks the other from later overwriting it, exactly as edge-cases.md's "Out-of-Order Webhook Delivery" requires.</summary>
    private static readonly Dictionary<PaymentStatus, int> Precedence = new()
    {
        [PaymentStatus.Initiated] = 0,
        [PaymentStatus.Pending] = 1,
        [PaymentStatus.Successful] = 2,
        [PaymentStatus.Failed] = 2,
        [PaymentStatus.Reconciled] = 3,
    };

    private PaymentTransaction()
    {
    }

    private PaymentTransaction(PaymentTransactionId id, PaymentId paymentId, string gatewayName, int attemptNumber, DateTimeOffset now)
    {
        Id = id;
        PaymentId = paymentId;
        GatewayName = gatewayName;
        AttemptNumber = attemptNumber;
        Status = PaymentStatus.Initiated;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public PaymentTransactionId Id { get; private init; }

    public PaymentId PaymentId { get; private init; }

    public string GatewayName { get; private set; } = string.Empty;

    public int AttemptNumber { get; private init; }

    /// <summary>The gateway's own session-init reference (e.g. SSLCommerz's <c>sessionkey</c>) - known immediately after <c>IPaymentGateway.InitiateAsync</c> returns, before any webhook arrives.</summary>
    public string? GatewaySessionReference { get; private set; }

    /// <summary>
    /// The gateway's own permanent transaction/validation id - carries its own DB-level uniqueness
    /// constraint (PaymentTransactionConfiguration; requirement-spec.md §4) so a duplicate or
    /// replayed-across-Payments webhook can never post twice. <c>null</c> until the FIRST webhook
    /// for this attempt reports it (a real SSLCommerz-style gateway only assigns this on
    /// validation, not at session-init).
    /// </summary>
    public string? GatewayTransactionId { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public bool IsNonTerminal => Status is PaymentStatus.Initiated or PaymentStatus.Pending;

    internal static PaymentTransaction StartFirstAttempt(PaymentId paymentId, string gatewayName, DateTimeOffset now) =>
        new(PaymentTransactionId.New(), paymentId, gatewayName, attemptNumber: 1, now);

    /// <summary>Called right after <c>IPaymentGateway.InitiateAsync</c> returns - moves this attempt to Pending (awaiting the gateway's own webhook confirmation).</summary>
    internal void RecordGatewaySessionReference(string sessionReference, DateTimeOffset now)
    {
        GatewaySessionReference = sessionReference;
        Status = PaymentStatus.Pending;
        UpdatedAt = now;
    }

    /// <summary>
    /// The idempotent, ordering-guarded state-transition function every caller (webhook handler,
    /// polling job) must go through. Returns <c>true</c> only if this call actually moved
    /// <see cref="Status"/> forward; a duplicate or backward-ordered report is a silent, safe no-op
    /// (edge-cases.md "Duplicate Webhook Delivery", "Out-of-Order Webhook Delivery").
    /// </summary>
    internal bool TryApplyGatewayStatus(string gatewayTransactionId, PaymentStatus reportedStatus, DateTimeOffset now)
    {
        if (Precedence[reportedStatus] <= Precedence[Status])
        {
            // Same-or-earlier tier than what is already recorded - a duplicate delivery (identical
            // status) or an out-of-order/conflicting one (e.g. Failed arriving after Successful
            // already committed). Never mutates, per §4's forward-only invariant.
            return false;
        }

        GatewayTransactionId ??= gatewayTransactionId;
        Status = reportedStatus;
        UpdatedAt = now;
        return true;
    }

    /// <summary>FIN-10: a Payment left in <c>Initiated</c> past a short timeout with no gateway session ever recorded (edge-cases.md "Gateway outage during POST /payments") is treated as Failed, the same as a stuck-Pending payment past the polling job's own timeout.</summary>
    internal void MarkStale(string reason, DateTimeOffset now)
    {
        Status = PaymentStatus.Failed;
        FailureReason = reason;
        UpdatedAt = now;
    }
}
