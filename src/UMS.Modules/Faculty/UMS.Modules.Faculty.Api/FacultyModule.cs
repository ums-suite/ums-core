using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Faculty.Api.Endpoints;

namespace UMS.Modules.Faculty.Api;

/// <summary>Faculty's endpoints register themselves under <c>/api/v1/faculty/...</c> (release/DEVELOPMENT_PLAN.md Flow #10), mirroring Organization's own <c>OrganizationModule</c> exactly.</summary>
public static class FacultyModule
{
    public static IEndpointRouteBuilder MapFacultyModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/faculty").WithTags("Faculty");

        group.MapFacultyMemberEndpoints();
        group.MapCourseAssignmentEndpoints();
        group.MapLeaveRequestEndpoints();
        group.MapResearchProfileEndpoints();

        return endpoints;
    }
}
