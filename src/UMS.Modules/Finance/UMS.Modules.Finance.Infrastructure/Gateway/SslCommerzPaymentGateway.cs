using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.Infrastructure.Gateway;

/// <summary>
/// FIN-4: ADR-0008's primary launch provider. Talks to <see cref="FakeSslCommerzPrimaryHandler"/>
/// (no real SSLCommerz sandbox credentials exist in this environment - the same documented posture
/// Notifications' own channel adapters take) through a genuinely real
/// <c>UMS.Shared.Resilience</c>-wrapped <c>HttpClient</c>, so the resilience pipeline itself is
/// real; only the "gateway" underneath it is simulated.
///
/// <para>
/// <see cref="VerifyWebhookSignature"/> is a REAL HMAC-SHA256 computation, not simulated away - the
/// one piece of ADR-0008 this build treats as non-negotiable to fake, since "signature-verified
/// before any state transition" is itself one of the domain invariants under test
/// (requirement-spec.md §4). A real SSLCommerz integration would verify against SSLCommerz's own
/// documented signing scheme instead; this build's fake gateway and this verifier share one
/// symmetric secret (<see cref="FakePaymentGatewayOptions.WebhookSigningSecret"/>) computed over the
/// exact raw request body, matching how a real webhook signature is always computed over
/// the literal bytes received, never a re-serialized object.
/// </para>
/// </summary>
internal sealed class SslCommerzPaymentGateway(HttpClient httpClient, IOptionsMonitor<FakePaymentGatewayOptions> optionsMonitor) : IPaymentGateway
{
    public string Name => "SSLCommerz";

    public async Task<GatewayInitiationResult> InitiateAsync(GatewayInitiationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await httpClient.PostAsJsonAsync(
            "/init",
            new { request.MerchantTransactionId, request.Amount, request.Currency, request.OwnerId, request.FeeType },
            cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"{Name} gateway rejected session initiation with {(int)response.StatusCode}: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<InitiationResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException($"{Name} gateway returned an empty initiation response.");

        return new GatewayInitiationResult(payload.SessionKey, payload.RedirectUrl);
    }

    public async Task<GatewayStatusQueryResult?> QueryStatusAsync(string merchantTransactionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/status/{Uri.EscapeDataString(merchantTransactionId)}", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<StatusResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new HttpRequestException($"{Name} gateway returned an empty status response.");

        return Enum.TryParse<PaymentStatus>(payload.Status, ignoreCase: true, out var status)
            ? new GatewayStatusQueryResult(payload.GatewayTransactionId, status)
            : null;
    }

    public bool VerifyWebhookSignature(string rawPayload, string? providedSignature)
    {
        if (string.IsNullOrEmpty(providedSignature))
        {
            return false;
        }

        var computedHex = WebhookSignatureCalculator.Compute(optionsMonitor.CurrentValue.WebhookSigningSecret, rawPayload);

        // Constant-time comparison - a webhook signature check must never leak timing information
        // about how many leading characters matched.
        var providedBytes = Encoding.UTF8.GetBytes(providedSignature.Trim().ToLowerInvariant());
        var computedBytes = Encoding.UTF8.GetBytes(computedHex);
        return providedBytes.Length == computedBytes.Length && CryptographicOperations.FixedTimeEquals(providedBytes, computedBytes);
    }

    private sealed record InitiationResponse(string SessionKey, string RedirectUrl);

    private sealed record StatusResponse(string GatewayTransactionId, string Status);
}
