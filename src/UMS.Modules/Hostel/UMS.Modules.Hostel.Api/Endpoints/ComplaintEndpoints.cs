using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Application.Complaints;
using UMS.Modules.Hostel.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Api.Endpoints;

/// <summary>HOS-15/16: requirement-spec.md §6 Complaint rows.</summary>
internal static class ComplaintEndpoints
{
    public static void MapComplaintEndpoints(this RouteGroupBuilder group)
    {
        var complaints = group.MapGroup("/complaints");

        complaints.MapPost("/", async (SubmitComplaintRequest body, HttpContext httpContext, StudentContextService studentContext, ComplaintService service, CancellationToken cancellationToken) =>
        {
            var studentId = await OwnershipGuard.ResolveOwnStudentIdAsync(studentContext, httpContext, cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.SubmitAsync(studentId.Value, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        complaints.MapGet("/me", async (HttpContext httpContext, StudentContextService studentContext, ComplaintService service, CancellationToken cancellationToken) =>
        {
            var studentId = await OwnershipGuard.ResolveOwnStudentIdAsync(studentContext, httpContext, cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.GetByStudentAsync(studentId.Value, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        complaints.MapPatch("/{id:guid}", async (Guid id, PatchComplaintRequest body, HttpContext httpContext, ComplaintService service, CancellationToken cancellationToken) =>
        {
            var result = await service.PatchAsync(id, body, httpContext.User.GetUserId(), httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.ComplaintResolve);
    }
}
