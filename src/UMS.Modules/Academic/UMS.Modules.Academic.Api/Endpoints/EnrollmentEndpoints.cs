using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Enrollments;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-6/ACD-7/ACD-8: Enrollment create ("Student-owned")/drop ("Student-owned")/approve (Advisor gate) (requirement-spec.md §6).</summary>
internal static class EnrollmentEndpoints
{
    public static void MapEnrollmentEndpoints(this RouteGroupBuilder group)
    {
        var enrollments = group.MapGroup("/enrollments");

        enrollments.MapPost("/", async (CreateEnrollmentRequest body, HttpContext httpContext, EnrollmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/academic/enrollments/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // DELETE requests don't support ASP.NET Core minimal API body inference - [FromBody] is
        // required here, or endpoint-route-table construction throws at startup for the WHOLE app
        // (every endpoint, not just this one), the exact way this was first caught.
        enrollments.MapDelete("/{id:guid}", async (Guid id, [FromBody] DropEnrollmentRequest body, HttpContext httpContext, EnrollmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DropAsync(httpContext.User.GetUserId(), id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        enrollments.MapPost("/{id:guid}/approve", async (Guid id, HttpContext httpContext, EnrollmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(id, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.EnrollmentApprove);

        enrollments.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, EnrollmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
