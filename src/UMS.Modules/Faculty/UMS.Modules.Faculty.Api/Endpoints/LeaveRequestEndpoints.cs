using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.Application.LeaveRequests;
using UMS.Modules.Faculty.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.Api.Endpoints;

/// <summary>
/// FAC-6..FAC-11: the LeaveRequest approval-chain workflow (requirement-spec.md faculty §2, §6).
/// The spec's single literal `.../approve` and `.../reject` routes are split into a
/// department-head step and an authority step, each gated by its own Permission string - see
/// <c>LeaveRequestService</c>'s own remarks for why.
/// </summary>
internal static class LeaveRequestEndpoints
{
    public static void MapLeaveRequestEndpoints(this RouteGroupBuilder group)
    {
        var leaveRequests = group.MapGroup("/leave-requests");

        leaveRequests.MapGet("/", async (Guid facultyMemberId, int? skip, int? take, string? lang, HttpContext httpContext, FacultyMemberService facultyMemberService, LeaveRequestService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var facultyMemberResult = await facultyMemberService.GetByIdAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
            if (facultyMemberResult.IsFailure)
            {
                return facultyMemberResult.Error!.ToProblemResult(httpContext);
            }

            var isOwner = facultyMemberResult.Value.UserId == httpContext.User.GetUserId();
            var isApprover = !isOwner
                && (await permissionResolver.HasPermissionAsync(httpContext.User, FacultyPermissions.LeaveApproveDepartment, cancellationToken).ConfigureAwait(false)
                    || await permissionResolver.HasPermissionAsync(httpContext.User, FacultyPermissions.LeaveApproveAuthority, cancellationToken).ConfigureAwait(false));

            if (!isOwner && !isApprover)
            {
                return Error.Forbidden("leaverequest.list_forbidden", "Only the owning FacultyMember or an approver may list these LeaveRequests.").ToProblemResult(httpContext);
            }

            var page = await service.ListByFacultyMemberAsync(facultyMemberId, skip ?? 0, take ?? 50, lang, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).RequireLiveSession();

        leaveRequests.MapPost("/", async (SubmitLeaveRequestRequest body, HttpContext httpContext, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitAsync(httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/faculty/leave-requests/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.LeaveCreate);

        leaveRequests.MapPost("/{id:guid}/approve/department-head", async (Guid id, VersionedRequestBody body, HttpContext httpContext, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveByDepartmentHeadAsync(id, httpContext.User.GetUserId(), body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.LeaveApproveDepartment);

        leaveRequests.MapPost("/{id:guid}/approve/authority", async (Guid id, VersionedRequestBody body, HttpContext httpContext, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ApproveByAuthorityAsync(id, httpContext.User.GetUserId(), body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.LeaveApproveAuthority);

        leaveRequests.MapPost("/{id:guid}/reject/department-head", async (Guid id, RejectLeaveRequestRequest body, HttpContext httpContext, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.LeaveApproveDepartment);

        leaveRequests.MapPost("/{id:guid}/reject/authority", async (Guid id, RejectLeaveRequestRequest body, HttpContext httpContext, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RejectAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(FacultyPermissions.LeaveApproveAuthority);

        leaveRequests.MapPost("/{id:guid}/cancel", async (Guid id, VersionedRequestBody body, HttpContext httpContext, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAsync(id, httpContext.User.GetUserId(), body.Version, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        leaveRequests.MapPost("/{id:guid}/supporting-document", async (Guid id, AttachSupportingDocumentRequest body, HttpContext httpContext, LeaveRequestService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AttachSupportingDocumentAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
