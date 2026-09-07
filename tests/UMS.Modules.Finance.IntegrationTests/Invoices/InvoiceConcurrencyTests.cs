using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Invoices;
using UMS.Modules.Finance.IntegrationTests.Infrastructure;

namespace UMS.Modules.Finance.IntegrationTests.Invoices;

/// <summary>FIN-2: edge-cases.md "Concurrent CreateInvoice Calls for the Same (sourceModule, sourceReferenceId, feeType) Tuple" - a genuine concurrent race (Task.WhenAll, not a sequential retry) against a real Postgres unique constraint.</summary>
[Collection(FinanceApiTestCollectionDefinition.Name)]
public sealed class InvoiceConcurrencyTests(FinanceServiceFixture fixture)
{
    [Fact]
    public async Task Two_genuinely_concurrent_CreateInvoice_calls_for_the_same_tuple_never_produce_two_rows()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var sourceReferenceId = $"app-{Guid.NewGuid():N}";

        async Task<InvoiceDto> CreateAsync()
        {
            using var scope = fixture.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<InvoiceService>();
            var result = await service.CreateAsync(new CreateInvoiceRequest("admission", sourceReferenceId, feeType, ownerId, null, null, Guid.NewGuid().ToString()));
            return result.Value;
        }

        var results = await Task.WhenAll(CreateAsync(), CreateAsync());

        Assert.Equal(results[0].Id, results[1].Id);

        using var scope = fixture.Services.CreateScope();
        var owned = await scope.ServiceProvider.GetRequiredService<IInvoiceRepository>().GetByOwnerAsync(ownerId);
        Assert.Single(owned);
    }

    [Fact]
    public async Task A_sequential_retry_for_an_already_existing_tuple_returns_the_existing_Invoice()
    {
        var feeType = $"ApplicationFee-{Guid.NewGuid():N}";
        await FinanceTestData.SeedFeeStructureAsync(fixture.Services, feeType);
        var ownerId = Guid.NewGuid();
        var sourceReferenceId = $"app-{Guid.NewGuid():N}";

        using var scope1 = fixture.Services.CreateScope();
        var first = await scope1.ServiceProvider.GetRequiredService<InvoiceService>()
            .CreateAsync(new CreateInvoiceRequest("admission", sourceReferenceId, feeType, ownerId, null, null, "corr-1"));

        using var scope2 = fixture.Services.CreateScope();
        var second = await scope2.ServiceProvider.GetRequiredService<InvoiceService>()
            .CreateAsync(new CreateInvoiceRequest("admission", sourceReferenceId, feeType, ownerId, null, null, "corr-2"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
    }
}
