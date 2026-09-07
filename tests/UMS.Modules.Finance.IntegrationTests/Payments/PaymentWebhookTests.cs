using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.Infrastructure.Gateway;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;

namespace UMS.Modules.Finance.IntegrationTests.Payments;

/// <summary>FIN-8: ADR-0008's signature verification, and edge-cases.md's "Duplicate Webhook Delivery" / "Out-of-Order Webhook Delivery Causing a Backward State Transition".</summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class PaymentWebhookTests(FinanceServiceFixture fixture)
{
    private const string WebhookSigningSecret = "integration-test-webhook-secret";

    [Fact]
    public async Task A_webhook_with_an_invalid_signature_is_rejected_and_never_mutates_the_Payment()
    {
        var payment = await InitiatePendingPaymentAsync();
        var body = BuildWebhookBody(payment.Id, "gw-tx-1", "Successful");

        using var scope = fixture.Services.CreateScope();
        var webhookService = scope.ServiceProvider.GetRequiredService<PaymentWebhookService>();
        var result = await webhookService.HandleAsync(payment.Id, body, "0000000000000000000000000000000000000000000000000000000000000000", "corr-1");

        Assert.True(result.IsFailure);
        Assert.Equal("payment.webhook_signature_invalid", result.Error!.Code);

        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, payment.OwnerId);
        Assert.Equal("Pending", reloaded.Value.Status);
    }

    [Fact]
    public async Task A_correctly_signed_webhook_reporting_Successful_transitions_the_Payment()
    {
        var payment = await InitiatePendingPaymentAsync();
        var body = BuildWebhookBody(payment.Id, "gw-tx-2", "Successful");
        var signature = WebhookSignatureCalculator.Compute(WebhookSigningSecret, body);

        using var scope = fixture.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<PaymentWebhookService>().HandleAsync(payment.Id, body, signature, "corr-2");

        Assert.True(result.IsSuccess);
        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, payment.OwnerId);
        Assert.Equal("Successful", reloaded.Value.Status);
    }

    /// <summary>edge-cases.md "Duplicate Webhook Delivery for the Same PaymentTransaction" - the second, identical delivery is a silent no-op.</summary>
    [Fact]
    public async Task A_duplicate_webhook_delivery_is_a_no_op_and_the_Payment_stays_Successful()
    {
        var payment = await InitiatePendingPaymentAsync();
        var body = BuildWebhookBody(payment.Id, "gw-tx-3", "Successful");
        var signature = WebhookSignatureCalculator.Compute(WebhookSigningSecret, body);

        using var scope = fixture.Services.CreateScope();
        var webhookService = scope.ServiceProvider.GetRequiredService<PaymentWebhookService>();
        var first = await webhookService.HandleAsync(payment.Id, body, signature, "corr-3a");
        var second = await webhookService.HandleAsync(payment.Id, body, signature, "corr-3b");

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, payment.OwnerId);
        Assert.Equal("Successful", reloaded.Value.Status);
    }

    /// <summary>edge-cases.md "Out-of-Order Webhook Delivery Causing a Backward State Transition" - a Failed report arriving after an already-committed Successful never flips it backward.</summary>
    [Fact]
    public async Task A_Failed_webhook_arriving_after_an_already_committed_Successful_never_flips_it_backward()
    {
        var payment = await InitiatePendingPaymentAsync();
        var successBody = BuildWebhookBody(payment.Id, "gw-tx-4", "Successful");
        var successSignature = WebhookSignatureCalculator.Compute(WebhookSigningSecret, successBody);

        using var scope = fixture.Services.CreateScope();
        var webhookService = scope.ServiceProvider.GetRequiredService<PaymentWebhookService>();
        await webhookService.HandleAsync(payment.Id, successBody, successSignature, "corr-4a");

        var failedBody = BuildWebhookBody(payment.Id, "gw-tx-4", "Failed");
        var failedSignature = WebhookSignatureCalculator.Compute(WebhookSigningSecret, failedBody);
        var result = await webhookService.HandleAsync(payment.Id, failedBody, failedSignature, "corr-4b");

        Assert.True(result.IsSuccess);
        var reloaded = await scope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, payment.OwnerId);
        Assert.Equal("Successful", reloaded.Value.Status);
    }

    private async Task<PaymentDto> InitiatePendingPaymentAsync()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var invoice = await FinanceTestData.SeedInvoiceAsync(fixture.Services, feeType, ownerId);

        using var scope = fixture.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<PaymentService>()
            .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, $"key-{Guid.NewGuid():N}"));

        Assert.True(result.IsSuccess);
        return result.Value.Payment;
    }

    private static string BuildWebhookBody(Guid paymentId, string gatewayTransactionId, string status) =>
        JsonSerializer.Serialize(new { merchantTransactionId = paymentId.ToString(), gatewayTransactionId, status });
}
