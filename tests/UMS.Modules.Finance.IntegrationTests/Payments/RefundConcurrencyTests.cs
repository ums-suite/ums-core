using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Common;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.IntegrationTests.Payments;

/// <summary>
/// design-decisions.md "Refund Concurrency Control (Amount Ceiling and Dispute-Hold Gate)": "DB-
/// enforced transactional check ... in the same transaction as the refund insert - not application-
/// level optimistic locking." A genuine concurrent race (Task.WhenAll), not a sequential retry - two
/// refund requests that would each individually be valid but together would overrun the Payment's
/// amount ceiling if the check-then-insert weren't serialized by the pessimistic row lock
/// RefundService.RefundAsync takes on the Payment row.
/// </summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class RefundConcurrencyTests(FinanceServiceFixture fixture)
{
    [Fact]
    public async Task Two_concurrent_refund_requests_that_together_would_overrun_the_ceiling_result_in_exactly_one_success()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        async Task<Result<RefundDto>> RefundAsync(string correlationId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<RefundService>();
            var audit = new AuditContext(Guid.NewGuid(), null, correlationId);
            return await service.RefundAsync(payment.Id, audit, new RefundPaymentRequest(300m, Reason: null));
        }

        // Two individually-valid 300 requests against a 500 Payment - only one can win; the DB-
        // enforced ceiling check must reject the other, never silently overrun the ceiling to 600.
        var results = await Task.WhenAll(RefundAsync("corr-race-1"), RefundAsync("corr-race-2"));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code == "refund.amount_exceeds_remaining");

        using var verifyScope = fixture.Services.CreateScope();
        var reloaded = await verifyScope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId);
        Assert.True(reloaded.IsSuccess);
    }

    [Fact]
    public async Task Two_concurrent_full_refund_requests_against_the_same_Payment_result_in_exactly_one_success()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        async Task<Result<RefundDto>> RefundAsync(string correlationId)
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<RefundService>();
            var audit = new AuditContext(Guid.NewGuid(), null, correlationId);
            return await service.RefundAsync(payment.Id, audit, new RefundPaymentRequest(500m, Reason: null));
        }

        var results = await Task.WhenAll(RefundAsync("corr-race-3"), RefundAsync("corr-race-4"));

        Assert.Single(results, r => r.IsSuccess);
        Assert.Single(results, r => r.IsFailure && r.Error!.Code is "refund.amount_exceeds_remaining" or "refund.payment_already_fully_refunded");
    }
}
