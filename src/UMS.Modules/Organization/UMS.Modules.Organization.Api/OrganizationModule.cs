using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Api.Endpoints;

namespace UMS.Modules.Organization.Api;

/// <summary>
/// Organization's endpoints register themselves under <c>/api/v1/organization/...</c>
/// (src/Host/Program.cs's own comment; release/DEVELOPMENT_PLAN.md Flow #6), mirroring Identity's
/// own <c>IdentityModule</c> exactly. The Host calls this one extension - it never maps an
/// Organization route directly itself.
/// </summary>
public static class OrganizationModule
{
    public static IEndpointRouteBuilder MapOrganizationModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/organization").WithTags("Organization");

        group.MapUniversityEndpoints();
        group.MapCampusEndpoints();
        group.MapFacultyEndpoints();
        group.MapDepartmentEndpoints();
        group.MapProgramEndpoints();
        group.MapDesignationEndpoints();
        group.MapFacilityEndpoints();
        group.MapHierarchyEndpoints();

        return endpoints;
    }
}
