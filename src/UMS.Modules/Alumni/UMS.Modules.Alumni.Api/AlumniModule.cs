using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Alumni.Api.Endpoints;

namespace UMS.Modules.Alumni.Api;

/// <summary>Alumni's endpoints register themselves under <c>/api/v1/alumni/...</c> (requirement-spec.md §6), mirroring every other module's own registration exactly.</summary>
public static class AlumniModule
{
    public static IEndpointRouteBuilder MapAlumniModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/alumni").WithTags("Alumni");

        group.MapAlumnusEndpoints();
        group.MapChapterEndpoints();
        group.MapJobEndpoints();
        group.MapDonationEndpoints();
        group.MapMentorshipEndpoints();
        group.MapAlumniEventEndpoints();

        return endpoints;
    }
}
