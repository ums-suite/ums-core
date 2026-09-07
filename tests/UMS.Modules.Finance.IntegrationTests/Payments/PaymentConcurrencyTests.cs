using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;

namespace UMS.Modules.Finance.IntegrationTests.Payments;

/// <summary>FIN-6: edge-cases.md "Two Concurrent Payment Attempts Against the Same Invoice (Different Idempotency Keys)" - a genuine concurrent race (Task.WhenAll), not a sequential retry, against the real pessimistic row lock + partial-unique-index backstop.</summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class PaymentConcurrencyTests(FinanceServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_Payment_initiations_against_the_same_Invoice_result_in_exactly_one_non_terminal_Payment()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var invoice = await FinanceTestData.SeedInvoiceAsync(fixture.Services, feeType, ownerId);

        async Task<UMS.Shared.ErrorHandling.Results.Result<InitiatePaymentResult>> InitiateAsync(string idempotencyKey)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<PaymentService>();
            return await service.InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, idempotencyKey));
        }

        var results = await Task.WhenAll(
            InitiateAsync($"key-{Guid.NewGuid():N}"),
            InitiateAsync($"key-{Guid.NewGuid():N}"));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "payment.attempt_already_in_progress");
    }

    [Fact]
    public async Task Two_calls_with_the_same_Idempotency_Key_return_the_same_Payment()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var invoice = await FinanceTestData.SeedInvoiceAsync(fixture.Services, feeType, ownerId);
        var idempotencyKey = $"key-{Guid.NewGuid():N}";

        using var scope1 = fixture.Services.CreateScope();
        var first = await scope1.ServiceProvider.GetRequiredService<PaymentService>()
            .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, idempotencyKey));

        using var scope2 = fixture.Services.CreateScope();
        var second = await scope2.ServiceProvider.GetRequiredService<PaymentService>()
            .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, idempotencyKey));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Payment.Id, second.Value.Payment.Id);
    }

    [Fact]
    public async Task Initiating_against_an_Invoice_that_already_has_a_non_terminal_Payment_is_rejected_sequentially_too()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var invoice = await FinanceTestData.SeedInvoiceAsync(fixture.Services, feeType, ownerId);

        using var scope1 = fixture.Services.CreateScope();
        var first = await scope1.ServiceProvider.GetRequiredService<PaymentService>()
            .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, $"key-{Guid.NewGuid():N}"));
        Assert.True(first.IsSuccess);

        using var scope2 = fixture.Services.CreateScope();
        var second = await scope2.ServiceProvider.GetRequiredService<PaymentService>()
            .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, $"key-{Guid.NewGuid():N}"));

        Assert.True(second.IsFailure);
        Assert.Equal("payment.attempt_already_in_progress", second.Error!.Code);
    }
}
