namespace UMS.Modules.Finance.Application.Payments;

/// <summary>
/// The webhook wire shape this build's fake gateway (and any real one) posts to
/// <c>POST /api/v1/finance/payments/{id}/gateway-webhook</c>. <see cref="Status"/> uses
/// <c>PaymentStatus</c>'s own names directly (<c>"Pending"</c>/<c>"Successful"</c>/
/// <c>"Failed"</c>) rather than a real gateway's own vocabulary (SSLCommerz's <c>VALID</c>/
/// <c>FAILED</c>/<c>CANCELLED</c>) - a documented simplification reasonable precisely because this
/// build's gateway is a fake (see <c>SslCommerzPaymentGateway</c>'s own remarks), the same
/// "identical shape either way" simplification Notifications' fake channel gateways already made.
/// The signature itself travels in the <c>X-Signature</c> header, computed over the exact raw
/// request body - never a field inside the body - so verification never depends on how this record
/// happens to (de)serialize (ADR-0008: "signature-verified before any state transition").
/// </summary>
public sealed record GatewayWebhookRequest(string MerchantTransactionId, string GatewayTransactionId, string Status);
