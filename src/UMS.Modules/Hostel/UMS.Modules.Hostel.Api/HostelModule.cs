using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Hostel.Api.Endpoints;

namespace UMS.Modules.Hostel.Api;

/// <summary>Hostel's endpoints register themselves under <c>/api/v1/hostel/...</c> (requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class HostelModule
{
    public static IEndpointRouteBuilder MapHostelModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/hostel").WithTags("Hostel");

        group.MapHostelEndpoints();
        group.MapApplicationWindowEndpoints();
        group.MapHostelApplicationEndpoints();
        group.MapAllocationEndpoints();
        group.MapComplaintEndpoints();

        return endpoints;
    }
}
