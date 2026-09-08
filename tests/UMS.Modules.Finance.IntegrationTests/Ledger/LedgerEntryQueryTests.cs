using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Ledger;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;

namespace UMS.Modules.Finance.IntegrationTests.Ledger;

/// <summary>FIN-13: <c>GET /api/v1/finance/ledger-entries</c> - requirement-spec.md §6, backed by rows Invoice/Payment/Refund already write in the same transaction as their own mutation (FIN-12).</summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class LedgerEntryQueryTests(FinanceServiceFixture fixture)
{
    [Fact]
    public async Task Ledger_entries_are_queryable_by_reference_after_an_Invoice_and_a_Payment_are_created()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LedgerEntryQueryService>();

        var forPayment = await service.ListAsync(new LedgerEntryFilter("Payment", payment.Id, null, null, null), skip: 0, take: 50);

        Assert.Equal(1, forPayment.TotalCount);
        Assert.Equal("PaymentPosted", Assert.Single(forPayment.Items).EntryType);
    }

    [Fact]
    public async Task A_refund_writes_its_own_LedgerEntry_alongside_the_original_PaymentPosted_entry()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        using (var refundScope = fixture.Services.CreateScope())
        {
            var audit = new UMS.Modules.Finance.Application.Common.AuditContext(Guid.NewGuid(), null, "corr-ledger-1");
            var refund = await refundScope.ServiceProvider.GetRequiredService<UMS.Modules.Finance.Application.Payments.RefundService>()
                .RefundAsync(payment.Id, audit, new UMS.Modules.Finance.Application.Payments.RefundPaymentRequest(100m, Reason: null));
            Assert.True(refund.IsSuccess);
        }

        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LedgerEntryQueryService>();
        var page = await service.ListAsync(new LedgerEntryFilter("Payment", payment.Id, null, null, null), skip: 0, take: 50);

        Assert.Equal(2, page.TotalCount);
        Assert.Contains(page.Items, e => e.EntryType == "PaymentPosted");
        Assert.Contains(page.Items, e => e.EntryType == "RefundPosted");
    }

    [Fact]
    public async Task Filtering_by_EntryType_returns_only_matching_rows()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        var ownerId = Guid.NewGuid();
        var payment = await FinanceTestData.SeedSuccessfulPaymentAsync(fixture.Services, feeType, ownerId, amount: 500m);

        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LedgerEntryQueryService>();

        var page = await service.ListAsync(
            new LedgerEntryFilter("Payment", payment.Id, UMS.Modules.Finance.Domain.Ledger.LedgerEntryType.InvoiceRaised, null, null),
            skip: 0,
            take: 50);

        // The InvoiceRaised entry is keyed by ReferenceType "Invoice", not "Payment" - filtering by
        // ReferenceType "Payment" AND EntryType InvoiceRaised together yields nothing.
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task ListAsync_clamps_take_to_a_sane_maximum()
    {
        using var scope = fixture.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<LedgerEntryQueryService>();

        var page = await service.ListAsync(new LedgerEntryFilter(null, null, null, null, null), skip: 0, take: 10_000);

        Assert.Equal(200, page.Take);
    }
}
