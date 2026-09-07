using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.CourseOfferings;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-3/ACD-4/ACD-5: CourseOffering create/manage/browse, Assessment configuration (requirement-spec.md §6).</summary>
internal static class CourseOfferingEndpoints
{
    public static void MapCourseOfferingEndpoints(this RouteGroupBuilder group)
    {
        var offerings = group.MapGroup("/course-offerings");

        offerings.MapPost("/", async (CreateCourseOfferingRequest body, HttpContext httpContext, CourseOfferingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/academic/course-offerings/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.CourseOfferingManage);

        // ACD-4: registration browsing - an informational read, open to any authenticated caller
        // (requirement-spec.md §6 "Authenticated (any role)").
        offerings.MapGet("/", async (Guid semester, CourseOfferingQueryService service, CancellationToken cancellationToken) =>
        {
            var items = await service.ListBySemesterAsync(semester, cancellationToken).ConfigureAwait(false);
            return Results.Ok(items);
        }).RequireLiveSession();

        offerings.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, CourseOfferingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        offerings.MapPost("/{id:guid}/instructor", async (Guid id, AssignInstructorRequest body, HttpContext httpContext, CourseOfferingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AssignInstructorAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.CourseOfferingManage);

        // ACD-5: Exam/Assessment configuration.
        offerings.MapPost("/{id:guid}/exams", async (Guid id, CreateExamRequest body, HttpContext httpContext, AssessmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AddExamAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.CourseOfferingManage);
    }
}
