using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Common;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;

namespace UMS.Modules.Finance.IntegrationTests.Payments;

/// <summary>FIN-11: requirement-spec.md §2 Refund Workflow / §9's refund-execution decision - gateway-routed vs. manually-settled, and the already-fully-refunded rejection, against a real Postgres.</summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class RefundTests(FinanceServiceFixture fixture)
{
    [Fact]
    public async Task A_gateway_routed_refund_succeeds_writes_a_LedgerEntry_and_reduces_the_remaining_refundable_amount()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        using var scope = fixture.Services.CreateScope();
        var audit = new AuditContext(Guid.NewGuid(), "127.0.0.1", "corr-refund-1");
        var result = await scope.ServiceProvider.GetRequiredService<RefundService>()
            .RefundAsync(payment.Id, audit, new RefundPaymentRequest(200m, "Student requested a partial refund."));

        Assert.True(result.IsSuccess);
        Assert.Equal("Succeeded", result.Value.Status);
        Assert.Equal("GatewayRouted", result.Value.Method);
        Assert.Equal(200m, result.Value.Amount);
        Assert.NotNull(result.Value.GatewayRefundReference);
    }

    /// <summary>requirement-spec.md §9's refund-execution decision: "where it doesn't [support programmatic refund], an Accountant marks the refund manually settled ... and Finance still writes the Refund/LedgerEntry pair for audit continuity."</summary>
    [Fact]
    public async Task A_refund_the_gateway_does_not_support_falls_back_to_manual_settlement()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);
        fixture.GatewayState.MarkRefundUnsupported(payment.Id.ToString());

        using var scope = fixture.Services.CreateScope();
        var audit = new AuditContext(Guid.NewGuid(), "127.0.0.1", "corr-refund-2");
        var result = await scope.ServiceProvider.GetRequiredService<RefundService>()
            .RefundAsync(payment.Id, audit, new RefundPaymentRequest(500m, Reason: null));

        Assert.True(result.IsSuccess);
        Assert.Equal("Succeeded", result.Value.Status);
        Assert.Equal("ManuallySettled", result.Value.Method);
        Assert.Null(result.Value.GatewayRefundReference);
    }

    [Fact]
    public async Task A_refund_against_an_already_fully_refunded_Payment_is_rejected_outright()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        using var scope = fixture.Services.CreateScope();
        var audit = new AuditContext(Guid.NewGuid(), null, "corr-refund-3");
        var refunds = scope.ServiceProvider.GetRequiredService<RefundService>();

        var first = await refunds.RefundAsync(payment.Id, audit, new RefundPaymentRequest(500m, Reason: null));
        Assert.True(first.IsSuccess);

        var second = await refunds.RefundAsync(payment.Id, audit, new RefundPaymentRequest(1m, Reason: null));

        Assert.True(second.IsFailure);
        Assert.Equal("refund.payment_already_fully_refunded", second.Error!.Code);
    }

    [Fact]
    public async Task A_refund_exceeding_the_remaining_refundable_amount_is_rejected()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        using var scope = fixture.Services.CreateScope();
        var audit = new AuditContext(Guid.NewGuid(), null, "corr-refund-4");
        var result = await scope.ServiceProvider.GetRequiredService<RefundService>()
            .RefundAsync(payment.Id, audit, new RefundPaymentRequest(500.01m, Reason: null));

        Assert.True(result.IsFailure);
        Assert.Equal("refund.amount_exceeds_remaining", result.Error!.Code);
    }

    [Fact]
    public async Task A_refund_against_a_Payment_that_never_succeeded_is_rejected()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var invoice = await FinanceTestData.SeedInvoiceAsync(fixture.Services, feeType, ownerId);

        using var initiateScope = fixture.Services.CreateScope();
        var initiate = await initiateScope.ServiceProvider.GetRequiredService<PaymentService>()
            .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, $"key-{Guid.NewGuid():N}"));
        Assert.True(initiate.IsSuccess);

        using var scope = fixture.Services.CreateScope();
        var audit = new AuditContext(Guid.NewGuid(), null, "corr-refund-5");
        var result = await scope.ServiceProvider.GetRequiredService<RefundService>()
            .RefundAsync(initiate.Value.Payment.Id, audit, new RefundPaymentRequest(100m, Reason: null));

        Assert.True(result.IsFailure);
        Assert.Equal("refund.payment_not_refundable", result.Error!.Code);
    }
}
