using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Career.Api.Endpoints;

namespace UMS.Modules.Career.Api;

/// <summary>Career's endpoints register themselves under <c>/api/v1/career/...</c> (requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class CareerModule
{
    public static IEndpointRouteBuilder MapCareerModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/career").WithTags("Career");

        group.MapEmployerEndpoints();
        group.MapInternshipEndpoints();
        group.MapDriveEndpoints();
        group.MapCareerApplicationEndpoints();
        group.MapResumeProfileEndpoints();

        return endpoints;
    }
}
