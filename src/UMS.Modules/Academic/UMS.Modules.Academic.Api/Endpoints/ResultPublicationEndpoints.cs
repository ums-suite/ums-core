using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Modules.Academic.Application.ResultPublications;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>
/// ACD-11/ACD-12: the Department-Head lock/reject review step, authority approval, publish, and
/// archive - grouped under <c>/results/{courseOfferingId}/...</c> for consistency (a deliberate,
/// documented deviation from requirement-spec.md §6's literal <c>/grades/{id}/lock</c> path - see
/// this module's own PR "Known deviations" section: these are batch/ResultPublication-granularity
/// operations, not single-Grade operations, and grouping them under the CourseOffering-keyed
/// <c>/results/</c> prefix already established by <c>POST /results/{courseOfferingId}/publish</c>
/// avoids the ambiguous per-Grade-vs-batch route naming the spec's own table leaves unresolved).
/// </summary>
internal static class ResultPublicationEndpoints
{
    public static void MapResultPublicationEndpoints(this RouteGroupBuilder group)
    {
        var results = group.MapGroup("/results");

        results.MapPost("/{courseOfferingId:guid}/lock", async (Guid courseOfferingId, HttpContext httpContext, ResultPublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.LockAsync(courseOfferingId, httpContext.User.GetUserId(), httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.GradeLock);

        results.MapPost("/{courseOfferingId:guid}/reject", async (Guid courseOfferingId, RejectGradeBatchRequest body, HttpContext httpContext, ResultPublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(courseOfferingId, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.GradeLock);

        results.MapPost("/{courseOfferingId:guid}/approve", async (Guid courseOfferingId, HttpContext httpContext, ResultPublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(courseOfferingId, httpContext.User.GetUserId(), httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.ResultApprove);

        results.MapPost("/{courseOfferingId:guid}/publish", async (Guid courseOfferingId, HttpContext httpContext, ResultPublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PublishAsync(courseOfferingId, httpContext.User.GetUserId(), httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.ResultPublish);

        results.MapPost("/{courseOfferingId:guid}/archive", async (Guid courseOfferingId, HttpContext httpContext, ResultPublicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ArchiveAsync(courseOfferingId, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.ResultPublish);
    }
}
