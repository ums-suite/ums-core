using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Courses;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-2: Course create/manage (requirement-spec.md §6 <c>POST /courses</c>).</summary>
internal static class CourseEndpoints
{
    public static void MapCourseEndpoints(this RouteGroupBuilder group)
    {
        var courses = group.MapGroup("/courses");

        courses.MapPost("/", async (CreateCourseRequest body, HttpContext httpContext, CourseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/academic/courses/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.CourseManage);

        courses.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, CourseService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
