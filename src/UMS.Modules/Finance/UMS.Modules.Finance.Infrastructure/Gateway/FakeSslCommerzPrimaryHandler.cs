using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace UMS.Modules.Finance.Infrastructure.Gateway;

/// <summary>See <see cref="FakeSslCommerzGatewayState"/>'s own remarks - this is the terminal handler registered as <c>SslCommerzPaymentGateway</c>'s typed <c>HttpClient</c>'s <c>PrimaryHttpMessageHandler</c>, with <c>UMS.Shared.Resilience</c>'s real <c>AddStandardResilienceHandler()</c> wrapped around it exactly as it would be around a real gateway's socket handler.</summary>
internal sealed class FakeSslCommerzPrimaryHandler(FakeSslCommerzGatewayState state, IOptionsMonitor<FakePaymentGatewayOptions> optionsMonitor) : HttpMessageHandler
{
    private static readonly Random Random = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var options = optionsMonitor.CurrentValue;

        var latencyMs = Random.Next(options.MinLatencyMs, Math.Max(options.MinLatencyMs, options.MaxLatencyMs) + 1);
        await Task.Delay(latencyMs, cancellationToken).ConfigureAwait(false);

        if (options.ForceOutage)
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                RequestMessage = request,
                Content = new StringContent("""{"error":"simulated_gateway_unavailable"}"""),
            };
        }

        if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/init")
        {
            var body = await request.Content!.ReadFromJsonAsync<FakeInitiationRequest>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing fake gateway initiation request body.");

            var (gatewayTransactionId, _) = state.RecordInitiation(body.MerchantTransactionId);
            return JsonResponse(HttpStatusCode.OK, new FakeInitiationResponse($"session-{Guid.NewGuid():N}", $"https://fake-sslcommerz-gateway.ums-suite.internal/pay/{gatewayTransactionId}"));
        }

        if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath.StartsWith("/status/", StringComparison.Ordinal) == true)
        {
            var merchantTransactionId = request.RequestUri.AbsolutePath["/status/".Length..];
            var found = state.Query(merchantTransactionId);
            return found is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request }
                : JsonResponse(HttpStatusCode.OK, new FakeStatusResponse(found.Value.GatewayTransactionId, found.Value.Status));
        }

        // FIN-11: requirement-spec.md §9's refund-execution decision - a 409 tells
        // SslCommerzPaymentGateway.RefundAsync "this method doesn't support a programmatic refund",
        // which it surfaces as `null` so RefundService falls back to manual settlement instead.
        if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/refund")
        {
            var body = await request.Content!.ReadFromJsonAsync<FakeRefundRequest>(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Missing fake gateway refund request body.");

            if (state.IsRefundUnsupported(body.MerchantTransactionId))
            {
                return JsonResponse(HttpStatusCode.Conflict, new { error = "refund_not_supported" });
            }

            return JsonResponse(HttpStatusCode.OK, new FakeRefundResponse($"fake-sslcommerz-refund-{Guid.NewGuid():N}", Succeeded: true, FailureReason: null));
        }

        // FIN-14: a batch-shaped read of everything this fake gateway last recorded/updated on the
        // requested UTC calendar day - see FakeSslCommerzGatewayState.GetSettlementReport's own remarks.
        if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath.StartsWith("/settlement/", StringComparison.Ordinal) == true)
        {
            var dateSegment = request.RequestUri.AbsolutePath["/settlement/".Length..];
            if (!DateOnly.TryParse(dateSegment, out var settlementDate))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { RequestMessage = request };
            }

            var report = state.GetSettlementReport(settlementDate)
                .Select(r => new FakeSettlementRecord(r.MerchantTransactionId, r.GatewayTransactionId, r.Status))
                .ToList();
            return JsonResponse(HttpStatusCode.OK, report);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
    }

    private static HttpResponseMessage JsonResponse<T>(HttpStatusCode statusCode, T body) =>
        new(statusCode) { Content = JsonContent.Create(body) };

    private sealed record FakeInitiationRequest(string MerchantTransactionId, decimal Amount, string Currency, Guid OwnerId, string FeeType);

    private sealed record FakeInitiationResponse(string SessionKey, string RedirectUrl);

    private sealed record FakeStatusResponse(string GatewayTransactionId, string Status);

    private sealed record FakeRefundRequest(string MerchantTransactionId, string GatewayTransactionId, decimal Amount, string Currency);

    private sealed record FakeRefundResponse(string RefundReference, bool Succeeded, string? FailureReason);

    private sealed record FakeSettlementRecord(string MerchantTransactionId, string GatewayTransactionId, string Status);
}
