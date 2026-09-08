using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Research.Api.Endpoints;

namespace UMS.Modules.Research.Api;

/// <summary>Research's endpoints register themselves under <c>/api/v1/research/...</c> (requirement-spec.md §6), mirroring every other module's own registration exactly. The public showcase (RES-13) is mapped as its own anonymous sub-group, never gated by the module-wide permission checks the rest of this group's endpoints declare individually.</summary>
public static class ResearchModule
{
    public static IEndpointRouteBuilder MapResearchModule(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/v1/research").WithTags("Research");

        group.MapFundingBodyEndpoints();
        group.MapGrantEndpoints();
        group.MapPublicationEndpoints();
        group.MapInstitutionalRepositoryEntryEndpoints();
        group.MapPublicShowcaseEndpoints();

        return endpoints;
    }
}
