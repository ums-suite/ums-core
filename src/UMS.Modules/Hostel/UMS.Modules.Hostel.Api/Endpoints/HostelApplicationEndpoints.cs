using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Hostel.Application.Applications;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Api.Endpoints;

/// <summary>HOS-3/5/6: requirement-spec.md §6 Application rows, plus the HOS-withdrawal gap-fill.</summary>
internal static class HostelApplicationEndpoints
{
    public static void MapHostelApplicationEndpoints(this RouteGroupBuilder group)
    {
        var applications = group.MapGroup("/applications");

        applications.MapPost("/", async (CreateHostelApplicationRequest body, HttpContext httpContext, StudentContextService studentContext, HostelApplicationService service, CancellationToken cancellationToken) =>
        {
            var studentId = await OwnershipGuard.ResolveOwnStudentIdAsync(studentContext, httpContext, cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.SubmitNewApplicationAsync(studentId.Value, body, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        applications.MapGet("/me", async (HttpContext httpContext, StudentContextService studentContext, HostelApplicationService service, CancellationToken cancellationToken) =>
        {
            var studentId = await OwnershipGuard.ResolveOwnStudentIdAsync(studentContext, httpContext, cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.GetByStudentAsync(studentId.Value, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        applications.MapGet("/", async (Guid applicationWindowId, string status, HostelApplicationReviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetQueueAsync(applicationWindowId, status, cancellationToken).ConfigureAwait(false))).RequirePermission(HostelPermissions.ApplicationReview);

        applications.MapPost("/{id:guid}/review", async (Guid id, ReviewHostelApplicationRequest body, HttpContext httpContext, HostelApplicationReviewService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ReviewAsync(id, body, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.ApplicationReview);

        applications.MapPost("/{id:guid}/withdraw", async (Guid id, HttpContext httpContext, StudentContextService studentContext, HostelApplicationService service, CancellationToken cancellationToken) =>
        {
            var studentId = await OwnershipGuard.ResolveOwnStudentIdAsync(studentContext, httpContext, cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var owned = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (owned.IsFailure)
            {
                return owned.Error!.ToProblemResult(httpContext);
            }

            var forbidden = OwnershipGuard.CheckOwnership(studentId.Value, owned.Value.StudentId, httpContext);
            if (forbidden is not null)
            {
                return forbidden;
            }

            var result = await service.WithdrawAsync(id, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.Ok() : result.Error!.ToProblemResult(httpContext);
        }).RequireLiveSession();
    }
}
