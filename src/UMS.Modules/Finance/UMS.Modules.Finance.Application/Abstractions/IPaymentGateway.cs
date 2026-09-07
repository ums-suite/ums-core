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
}

/// <param name="MerchantTransactionId">Finance's OWN reference for this attempt (its Payment id) - passed to the gateway so its webhook can echo it back, letting Finance locate the right Payment without depending on the gateway assigning an id up front.</param>
public sealed record GatewayInitiationRequest(string MerchantTransactionId, decimal Amount, string Currency, Guid OwnerId, string FeeType);

/// <param name="SessionReference">The gateway's own session/init reference (e.g. SSLCommerz's <c>sessionkey</c>).</param>
/// <param name="RedirectUrl">Where the payer's browser is sent to complete payment at the gateway.</param>
public sealed record GatewayInitiationResult(string SessionReference, string RedirectUrl);

public sealed record GatewayStatusQueryResult(string GatewayTransactionId, PaymentStatus Status);
