using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Faculty.Application.CourseAssignments;
using UMS.Modules.Faculty.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Faculty.Api.Endpoints;

/// <summary>FAC-5: teaching-load / "Assigned Courses" read (requirement-spec.md faculty §2, §6).</summary>
internal static class CourseAssignmentEndpoints
{
    public static void MapCourseAssignmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/course-assignments", async (Guid facultyMemberId, CourseAssignmentQueryService service, CancellationToken cancellationToken) =>
        {
            var items = await service.ListByFacultyMemberAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(items);
        }).RequirePermission(FacultyPermissions.CourseAssignmentRead);
    }
}
