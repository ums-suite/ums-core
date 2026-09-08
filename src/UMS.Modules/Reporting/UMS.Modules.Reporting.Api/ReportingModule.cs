using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Reporting.Api.Endpoints;

namespace UMS.Modules.Reporting.Api;

/// <summary>Reporting's endpoints register themselves under <c>/api/v1/reporting/...</c> (requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class ReportingModule
{
    public static IEndpointRouteBuilder MapReportingModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/reporting").WithTags("Reporting");

        group.MapDashboardEndpoints();
        group.MapRegulatoryReportDefinitionEndpoints();
        group.MapRegulatoryReportRunEndpoints();

        return endpoints;
    }
}
