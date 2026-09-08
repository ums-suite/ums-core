using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.Application.Abstractions;

/// <summary>
/// ADR-0008: "All payment integration is owned exclusively by the Finance module behind an
/// IPaymentGateway abstraction, with SSLCommerz as the primary launch provider ... and bKash/Nagad
/// as direct, pluggable alternatives behind the same interface." No calling module ever sees this
/// interface - it is Finance's own Application-layer abstraction, implemented by Infrastructure
/// (mirrors Learning's own <c>IPlagiarismCheckProvider</c>/Notifications' own <c>IChannelProvider</c>
/// shape: an Application-owned port, an Infrastructure-owned HTTP adapter).
/// </summary>
public interface IPaymentGateway
{
    public string Name { get; }

    /// <summary>Starts a gateway session for one Payment attempt. Must return promptly (requirement-spec.md §5: "the initiation call itself must still return promptly, with the actual completion arriving asynchronously via webhook").</summary>
    public Task<GatewayInitiationResult> InitiateAsync(GatewayInitiationRequest request, CancellationToken cancellationToken = default);

    /// <summary>ADR-0008: "Webhook callbacks are signature-verified before any state transition" - checked BEFORE the payload is trusted for anything, including looking up the Payment it claims to describe.</summary>
    public bool VerifyWebhookSignature(string rawPayload, string? providedSignature);

    /// <summary>FIN-9: the stuck-payment sweep's own fallback path when no webhook ever arrives (edge-cases.md "Webhook never arrives") - queries the gateway directly for whatever it currently knows about this attempt. <c>null</c> means the gateway has no record of it at all (FIN-10's "the InitiateAsync call itself never reached the gateway" case).</summary>
    public Task<GatewayStatusQueryResult?> QueryStatusAsync(string merchantTransactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FIN-11: requirement-spec.md §9's refund-execution decision - "Refunds route through
    /// IPaymentGateway's refund capability where the provider supports it; where it doesn't, an
    /// Accountant marks the refund manually settled." <c>null</c> means this gateway/payment method
    /// does NOT support a programmatic refund for this transaction - the caller (<c>RefundService</c>)
    /// falls back to recording a manually-settled Refund instead; it is never itself a failure.
    /// </summary>
    public Task<GatewayRefundResult?> RefundAsync(GatewayRefundRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// FIN-14: the daily reconciliation job's own batch-shaped read of the gateway's settlement
    /// report for one calendar day - the same fake, in-process gateway state <see cref="QueryStatusAsync"/>
    /// already reads from, just shaped for reconciliation's actual comparison need (a whole day's
    /// worth of transactions at once) rather than one merchantTransactionId at a time.
    /// </summary>
    public Task<IReadOnlyList<GatewaySettlementRecord>> GetSettlementReportAsync(DateOnly settlementDate, CancellationToken cancellationToken = default);
}

/// <param name="MerchantTransactionId">Finance's OWN reference for this attempt (its Payment id) - passed to the gateway so its webhook can echo it back, letting Finance locate the right Payment without depending on the gateway assigning an id up front.</param>
public sealed record GatewayInitiationRequest(string MerchantTransactionId, decimal Amount, string Currency, Guid OwnerId, string FeeType);

/// <param name="SessionReference">The gateway's own session/init reference (e.g. SSLCommerz's <c>sessionkey</c>).</param>
/// <param name="RedirectUrl">Where the payer's browser is sent to complete payment at the gateway.</param>
public sealed record GatewayInitiationResult(string SessionReference, string RedirectUrl);

public sealed record GatewayStatusQueryResult(string GatewayTransactionId, PaymentStatus Status);

public sealed record GatewayRefundRequest(string MerchantTransactionId, string GatewayTransactionId, decimal Amount, string Currency);

/// <param name="GatewayRefundReference">The gateway's own refund reference - independent of <see cref="GatewayStatusQueryResult.GatewayTransactionId"/>, the original charge's own id.</param>
/// <param name="Succeeded">A refund attempt genuinely routed to the gateway can still itself fail (e.g. the gateway rejects it) - distinct from <c>null</c>/"not supported" on <see cref="IPaymentGateway.RefundAsync"/> itself.</param>
public sealed record GatewayRefundResult(string GatewayRefundReference, bool Succeeded, string? FailureReason);

public sealed record GatewaySettlementRecord(string MerchantTransactionId, string GatewayTransactionId, PaymentStatus Status);
