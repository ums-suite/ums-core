namespace UMS.Modules.Finance.Infrastructure.Gateway;

/// <summary>
/// Mirrors Notifications' own <c>FakeProviderGatewayOptions</c> shape exactly. <see cref="WebhookSigningSecret"/>
/// is the one addition specific to Finance's own gateway: ADR-0008's "signature-verified before any
/// state transition" is a REAL HMAC-SHA256 computation in this build (not simulated away), keyed by
/// this shared secret - both <see cref="FakeSslCommerzGatewayState"/> (when a test asks it to
/// produce a signed synthetic webhook) and <see cref="SslCommerzPaymentGateway.VerifyWebhookSignature"/>
/// use the identical value, exactly as Finance and a real SSLCommerz merchant dashboard would share
/// one configured secret.
/// </summary>
public sealed class FakePaymentGatewayOptions
{
    public int MinLatencyMs { get; set; } = 20;

    public int MaxLatencyMs { get; set; } = 80;

    public bool ForceOutage { get; set; }

    public string WebhookSigningSecret { get; set; } = "local-dev-only-fake-sslcommerz-webhook-secret";
}
