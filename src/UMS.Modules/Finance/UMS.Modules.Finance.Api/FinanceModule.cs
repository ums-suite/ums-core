using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Finance.Api.Endpoints;

namespace UMS.Modules.Finance.Api;

/// <summary>Finance's endpoints register themselves under <c>/api/v1/finance/...</c> (release/DEVELOPMENT_PLAN.md Flow #14; requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class FinanceModule
{
    public static IEndpointRouteBuilder MapFinanceModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/finance").WithTags("Finance");

        group.MapFeeStructureEndpoints();
        group.MapInvoiceEndpoints();
        group.MapPaymentEndpoints();
        group.MapLedgerEntryEndpoints();

        return endpoints;
    }
}
