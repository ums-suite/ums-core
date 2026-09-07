using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Finance.Application.FeeStructures;
using UMS.Modules.Finance.Application.Invoices;

namespace UMS.Modules.Finance.IntegrationTests.Infrastructure;

/// <summary>Seeds a real, Active FeeStructure and a real Invoice against it through the actual Application services - never raw SQL, so every test exercises the same invariants a real caller would.</summary>
internal static class FinanceTestData
{
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
}
