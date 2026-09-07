using System.Security.Cryptography;
using System.Text;

namespace UMS.Modules.Finance.Infrastructure.Gateway;

/// <summary>
/// The one place the HMAC-SHA256 webhook-signing computation lives, shared by
/// <see cref="SslCommerzPaymentGateway.VerifyWebhookSignature"/> and - deliberately public - by
/// integration tests and manual-verification tooling that need to craft a genuinely
/// correctly-signed synthetic webhook payload (mirroring a real SSLCommerz merchant's own signing
/// call), plus an intentionally-wrong one to confirm rejection.
/// </summary>
public static class WebhookSignatureCalculator
{
    public static string Compute(string secret, string rawPayload)
    {
        var computed = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(rawPayload));
        return Convert.ToHexStringLower(computed);
    }
}
