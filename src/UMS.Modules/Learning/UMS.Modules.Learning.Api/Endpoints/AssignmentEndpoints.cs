using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Learning.Api.Endpoints;

/// <summary>
/// LRN-1/LRN-2/LRN-3/LRN-4: requirement-spec.md learning §6's Assignment rows.
///
/// <para>
/// Every write here carries its <c>learning.assignment.*</c> permission AND an Instructor
/// resource-ownership check inside the service (§6's own note: a Department Head's <c>ScopeGrant</c>
/// covers oversight reads across their Department, never write access to another Instructor's
/// Assignment). The permission string alone is deliberately not sufficient - the same two-layer
/// shape Faculty's <c>LeaveRequest</c> approve/reject and Academic's attendance gate already use.
/// </para>
/// </summary>
internal static class AssignmentEndpoints
{
    public static void MapAssignmentEndpoints(this RouteGroupBuilder group)
    {
        var assignments = group.MapGroup("/assignments");

        assignments.MapPost("/", async (CreateAssignmentRequest body, HttpContext httpContext, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/learning/assignments/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.AssignmentManage);

        assignments.MapPost("/{id:guid}/publish", async (Guid id, HttpContext httpContext, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.AssignmentManage);

        assignments.MapPost("/{id:guid}/close", async (Guid id, HttpContext httpContext, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CloseAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.AssignmentManage);

        assignments.MapPost("/{id:guid}/cancel", async (Guid id, CancelAssignmentRequest body, HttpContext httpContext, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAsync(id, httpContext.User.GetUserId(), body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.AssignmentManage);

        assignments.MapPost("/{id:guid}/extensions", async (Guid id, GrantSubmissionExtensionRequest body, HttpContext httpContext, AssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GrantExtensionAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/learning/assignments/{id}/extensions/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(LearningPermissions.AssignmentExtensionGrant);

        // requirement-spec.md §6: "Authenticated (enrolled Student or Instructor)". Not expressible
        // as a static permission policy, so the enrollment/instructor scoping happens inside the
        // query service against Academic's own lookup - the same shape Academic's own
        // StudentResultEndpoints uses for its "named permission OR self-owned" pair.
        assignments.MapGet("/", async (Guid courseOfferingId, HttpContext httpContext, AssignmentQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ListByCourseOfferingAsync(courseOfferingId, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        assignments.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, AssignmentQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
