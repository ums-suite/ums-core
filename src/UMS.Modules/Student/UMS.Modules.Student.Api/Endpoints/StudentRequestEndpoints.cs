using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Student.Application.Permissions;
using UMS.Modules.Student.Application.StudentRequests;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Api.Endpoints;

/// <summary>
/// STU-9..STU-14 (requirement-spec.md student §2 Student-Initiated Requests, §6). The spec's single
/// literal <c>POST /students/requests</c> route covers all three request types via
/// <see cref="SubmitStudentRequestRequest.RequestType"/>'s discriminator - dispatched here to
/// <see cref="StudentRequestService"/>'s three separate submit methods, never one generic "create"
/// method, so each type's own distinct validation/routing rules (Suspended-Student restriction,
/// grievance scope resolution) stay in their own clearly-named call.
/// </summary>
internal static class StudentRequestEndpoints
{
    public static void MapStudentRequestEndpoints(this RouteGroupBuilder group)
    {
        var requests = group.MapGroup("/students/requests");

        requests.MapPost("/", async (SubmitStudentRequestRequest body, HttpContext httpContext, StudentRequestService service, CancellationToken cancellationToken) =>
        {
            var callerUserId = httpContext.User.GetUserId();
            var audit = httpContext.GetAuditContext();

            Result<StudentRequestDto> result = body.RequestType switch
            {
                "IdReissue" => await service.SubmitIdReissueAsync(callerUserId, new SubmitIdReissueRequest(body.Reason ?? string.Empty), audit, cancellationToken).ConfigureAwait(false),
                "TranscriptRequest" => await service.SubmitTranscriptRequestAsync(callerUserId, new SubmitTranscriptRequestRequest(body.Purpose ?? string.Empty), audit, cancellationToken).ConfigureAwait(false),
                "Grievance" => await service.SubmitGrievanceAsync(callerUserId, new SubmitGrievanceRequest(body.Description ?? string.Empty, body.IsAgainstOwnDepartmentHead), audit, cancellationToken).ConfigureAwait(false),
                _ => Error.Validation("studentrequest.invalid_type", $"'{body.RequestType}' is not a valid StudentRequest type - expected IdReissue, TranscriptRequest, or Grievance."),
            };

            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/student/students/requests/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        requests.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, StudentRequestService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var callerHasReviewPermission = await permissionResolver.HasPermissionAsync(httpContext.User, StudentPermissions.RequestReview, cancellationToken).ConfigureAwait(false);
            var result = await service.GetByIdAsync(id, httpContext.User.GetUserId(), callerHasReviewPermission, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        requests.MapPost("/{id:guid}/approve", async (Guid id, VersionedRequestBody body, HttpContext httpContext, StudentRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveAsync(id, httpContext.User.GetUserId(), body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(StudentPermissions.RequestReview);

        requests.MapPost("/{id:guid}/reject", async (Guid id, RejectStudentRequestRequest body, HttpContext httpContext, StudentRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(StudentPermissions.RequestReview);
    }
}
