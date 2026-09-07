using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Payments;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Modules.Finance.Infrastructure.Persistence;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;

namespace UMS.Modules.Finance.IntegrationTests.Payments;

/// <summary>FIN-9/FIN-10: edge-cases.md "Webhook never arrives" / "Gateway outage during POST /payments" - the stuck-payment sweep's own path to the identical transition function the live webhook handler uses.</summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class StuckPaymentSweepTests(FinanceServiceFixture fixture)
{
    [Fact]
    public async Task The_sweep_resolves_a_stuck_Pending_Payment_the_fake_gateway_now_reports_Successful_for()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var invoice = await FinanceTestData.SeedInvoiceAsync(fixture.Services, feeType, ownerId);

        PaymentDto payment;
        using (var initiateScope = fixture.Services.CreateScope())
        {
            var initiate = await initiateScope.ServiceProvider.GetRequiredService<PaymentService>()
                .InitiateAsync(ownerId, new InitiatePaymentRequest(invoice.Id, $"key-{Guid.NewGuid():N}"));
            Assert.True(initiate.IsSuccess);
            payment = initiate.Value.Payment;
        }

        // FIN-9's own 10-minute poll threshold: a Payment this fresh isn't yet a sweep candidate.
        // Backdating updated_at directly is TEST SETUP ONLY (simulating real wall-clock time
        // passing without a test actually waiting 10 real minutes) - never a production write path.
        await BackdateUpdatedAtAsync(payment.Id, TimeSpan.FromMinutes(15));

        // Simulates the gateway itself later confirming success - the same test-only hook a real
        // sandbox's own dashboard/API would answer through if this were a genuine SSLCommerz
        // account (FakeSslCommerzGatewayState's own remarks).
        fixture.GatewayState.SetStatus(payment.Id.ToString(), "Successful");

        // A fresh scope (fresh DbContext, empty change tracker) for the sweep itself - the
        // Payment's `Initiated`/tracked instance from the scope above must never be what the
        // sweep's own candidate scan resolves against; it must read what is actually in Postgres.
        using var sweepScope = fixture.Services.CreateScope();
        var processed = await sweepScope.ServiceProvider.GetRequiredService<StuckPaymentSweepService>().SweepAsync(batchSize: 50);

        Assert.Equal(1, processed);
        var reloaded = await sweepScope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(payment.Id, ownerId);
        Assert.Equal("Successful", reloaded.Value.Status);
    }

    /// <summary>
    /// FIN-10: no gateway record at all past the longer 30-minute stale threshold is given up on
    /// outright. <see cref="PaymentService.InitiateAsync"/> always tries the (fake) gateway
    /// immediately and this fake gateway always answers - so a genuinely-stuck <c>Initiated</c> row
    /// with no gateway session is instead seeded directly through the same domain factory
    /// (<see cref="Payment.Initiate"/>) and repository <see cref="PaymentService"/> itself uses,
    /// simulating the real scenario this edge case describes: a process crash between the DB insert
    /// and the gateway HTTP call actually going out.
    /// </summary>
    [Fact]
    public async Task A_stale_Initiated_Payment_with_no_gateway_record_at_all_is_marked_Failed()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var invoice = await FinanceTestData.SeedInvoiceAsync(fixture.Services, feeType, ownerId);

        Guid paymentId;
        using (var seedScope = fixture.Services.CreateScope())
        {
            var invoices = seedScope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
            var payments = seedScope.ServiceProvider.GetRequiredService<IPaymentRepository>();
            var unitOfWork = seedScope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var loadedInvoice = await invoices.GetByIdAsync(new UMS.Modules.Finance.Domain.Invoices.InvoiceId(invoice.Id));
            var created = Payment.Initiate(loadedInvoice!, ownerId, $"key-{Guid.NewGuid():N}", "SSLCommerz", DateTimeOffset.UtcNow.AddMinutes(-31));
            Assert.True(created.IsSuccess);
            paymentId = created.Value.Id.Value;

            payments.Add(created.Value);
            await unitOfWork.SaveChangesAsync();
        }

        // Never registered with the fake gateway at all - the InitiateAsync call itself never
        // reached it, exactly FIN-10's own described case.
        Assert.Null(fixture.GatewayState.Query(paymentId.ToString()));
        await BackdateUpdatedAtAsync(paymentId, TimeSpan.FromMinutes(31));

        using var sweepScope = fixture.Services.CreateScope();
        var processed = await sweepScope.ServiceProvider.GetRequiredService<StuckPaymentSweepService>().SweepAsync(batchSize: 50);

        Assert.Equal(1, processed);
        var reloaded = await sweepScope.ServiceProvider.GetRequiredService<PaymentService>().GetByIdAsync(paymentId, ownerId);
        Assert.Equal("Failed", reloaded.Value.Status);
    }

    [Fact]
    public async Task SweepAsync_runs_cleanly_with_no_candidates()
    {
        using var scope = fixture.Services.CreateScope();
        var processed = await scope.ServiceProvider.GetRequiredService<StuckPaymentSweepService>().SweepAsync(batchSize: 50);

        Assert.Equal(0, processed);
    }

    private async Task BackdateUpdatedAtAsync(Guid paymentId, TimeSpan age)
    {
        var backdatedTo = DateTimeOffset.UtcNow - age;
        using var scope = fixture.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var affected = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE finance.payments SET updated_at = {backdatedTo} WHERE id = {paymentId}");
        Assert.Equal(1, affected);
    }
}
