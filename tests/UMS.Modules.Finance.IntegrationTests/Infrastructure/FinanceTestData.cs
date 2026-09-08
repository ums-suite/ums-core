using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.FeeStructures;
using UMS.Modules.Finance.Application.Invoices;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.Infrastructure.Gateway;

namespace UMS.Modules.Finance.IntegrationTests.Infrastructure;

/// <summary>Seeds a real, Active FeeStructure and a real Invoice against it through the actual Application services - never raw SQL, so every test exercises the same invariants a real caller would.</summary>
internal static class FinanceTestData
{
    private const string WebhookSigningSecret = "integration-test-webhook-secret";

    public static async Task<FeeStructureDto> SeedFeeStructureAsync(IServiceProvider services, string feeType, decimal amount = 500m)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<FeeStructureService>();
        var result = await service.CreateAsync(new CreateFeeStructureRequest(feeType, "Service", null, feeType, amount, null, null)).ConfigureAwait(false);
        return result.Value;
    }

    public static async Task<InvoiceDto> SeedInvoiceAsync(IServiceProvider services, string feeType, Guid ownerId, string? sourceReferenceId = null)
    {
        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<InvoiceService>();
        var result = await service.CreateAsync(new CreateInvoiceRequest("admission", sourceReferenceId ?? $"app-{Guid.NewGuid():N}", feeType, ownerId, null, null, Guid.NewGuid().ToString())).ConfigureAwait(false);
        return result.Value;
    }

    /// <summary>
    /// FIN-11/FIN-14: both Refund and reconciliation tests need a genuinely Successful Payment to
    /// start from - seeded through the real Application services end to end (Invoice -> initiate ->
    /// signed webhook), never a raw SQL status flip, so both suites exercise the same code path a
    /// real caller would.
    ///
    /// <para>
    /// Unless overridden, the synthetic webhook's own <c>gatewayTransactionId</c> defaults to
    /// whatever <see cref="FakeSslCommerzGatewayState"/> already assigned this Payment during its own
    /// real <c>InitiateAsync</c>/<c>/init</c> call - the same identifier the fake gateway's own
    /// settlement report (<see cref="FakeSslCommerzGatewayState.GetSettlementReport"/>) will later
    /// echo back, so a reconciliation "match" scenario needs no extra alignment step.
    /// </para>
    /// </summary>
    public static async Task<PaymentDto> SeedSuccessfulPaymentAsync(IServiceProvider services, string feeType, Guid ownerId, decimal amount = 500m, string? gatewayTransactionId = null)
    {
        await SeedFeeStructureAsync(services, feeType, amount).ConfigureAwait(false);
        var invoice = await SeedInvoiceAsync(services, feeType, ownerId).ConfigureAwait(false);

        using var scope = services.CreateScope();
        var initiate = await scope.ServiceProvider.GetRequiredService<PaymentService>()
            .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, $"key-{Guid.NewGuid():N}"))
            .ConfigureAwait(false);

        var payment = initiate.Value.Payment;
        var gatewayState = scope.ServiceProvider.GetRequiredService<FakeSslCommerzGatewayState>();
        var effectiveGatewayTransactionId = gatewayTransactionId ?? gatewayState.Query(payment.Id.ToString())?.GatewayTransactionId ?? $"gw-tx-{Guid.NewGuid():N}";
        var body = JsonSerializer.Serialize(new { merchantTransactionId = payment.Id.ToString(), gatewayTransactionId = effectiveGatewayTransactionId, status = "Successful" });
        var signature = WebhookSignatureCalculator.Compute(WebhookSigningSecret, body);

        var webhookResult = await scope.ServiceProvider.GetRequiredService<PaymentWebhookService>()
            .HandleAsync(payment.Id, body, signature, "seed-successful-payment")
            .ConfigureAwait(false);

        if (webhookResult.IsFailure)
        {
            throw new InvalidOperationException($"Failed to seed a Successful Payment: {webhookResult.Error}");
        }

        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId).ConfigureAwait(false);
        return reloaded.Value;
    }
}
