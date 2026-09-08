using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Library.Api.Endpoints;

namespace UMS.Modules.Library.Api;

/// <summary>Library's endpoints register themselves under <c>/api/v1/library/...</c> (requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class LibraryModule
{
    public static IEndpointRouteBuilder MapLibraryModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/library").WithTags("Library");

        group.MapBookEndpoints();
        group.MapLoanEndpoints();
        group.MapReservationEndpoints();
        group.MapFineEndpoints();
        group.MapDigitalResourceEndpoints();

        return endpoints;
    }
}
