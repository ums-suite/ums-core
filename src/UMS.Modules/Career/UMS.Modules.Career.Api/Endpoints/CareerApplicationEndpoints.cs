using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Student;

namespace UMS.Modules.Career.Api.Endpoints;

/// <summary>CAR-7/8: CareerApplication status review (either target's staff path) and Student-initiated withdrawal (requirement-spec.md §2.4, §6 `/career/applications`).</summary>
internal static class CareerApplicationEndpoints
{
    public static void MapCareerApplicationEndpoints(this RouteGroupBuilder group)
    {
        var applications = group.MapGroup("/applications");

        // requirement-spec.md §6: "Student's own applications; staff view with scope filters" - a
        // Career-Services-staff/Admin caller passing internshipId/driveId gets that scoped review
        // list; every other caller (or a staff caller passing neither filter) gets their own.
        applications.MapGet("/", async (Guid? internshipId, Guid? driveId, HttpContext httpContext, CareerApplicationReviewService service, IPermissionResolver permissionResolver, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            if ((internshipId is not null || driveId is not null) && await HasEitherManagePermissionAsync(httpContext, permissionResolver, cancellationToken).ConfigureAwait(false))
            {
                if (internshipId is { } id)
                {
                    return Results.Ok(await service.ListByInternshipAsync(id, cancellationToken).ConfigureAwait(false));
                }

                return Results.Ok(await service.ListByDriveAsync(driveId!.Value, cancellationToken).ConfigureAwait(false));
            }

            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.ListByStudentAsync(studentId.Value, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        applications.MapGet("/mine", async (HttpContext httpContext, CareerApplicationReviewService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            return Results.Ok(await service.ListByStudentAsync(studentId.Value, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        applications.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, CareerApplicationReviewService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result.Error!.ToProblemResult(httpContext);
            }

            if (await IsOwnerOrStaffAsync(result.Value.StudentId, httpContext, permissionResolver, cancellationToken).ConfigureAwait(false))
            {
                return Results.Ok(result.Value);
            }

            return Error.Forbidden("careerapplication.not_owner", "Only the owning Student or Career-Services staff may view this CareerApplication.").ToProblemResult(httpContext);
        }).RequireLiveSession();

        // CAR-7: staff status review - either manage permission is accepted since a CareerApplication may target an Internship or a Drive.
        applications.MapPost("/{id:guid}/status", async (Guid id, ChangeCareerApplicationStatusRequest body, HttpContext httpContext, CareerApplicationReviewService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            if (!await HasEitherManagePermissionAsync(httpContext, permissionResolver, cancellationToken).ConfigureAwait(false))
            {
                return Error.Forbidden("careerapplication.not_staff", "Only Career-Services staff/Admin may change a CareerApplication's status.").ToProblemResult(httpContext);
            }

            var result = await service.ChangeStatusAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // CAR-8: Student-initiated withdrawal - available before any terminal status, no cross-application side effects.
        applications.MapPost("/{id:guid}/withdraw", async (Guid id, uint version, HttpContext httpContext, CareerApplicationReviewService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.WithdrawAsync(id, studentId.Value, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }

    private static async Task<bool> IsOwnerOrStaffAsync(Guid ownerStudentId, HttpContext httpContext, IPermissionResolver permissionResolver, CancellationToken cancellationToken)
    {
        var studentStatusChecker = httpContext.RequestServices.GetRequiredService<IStudentStatusChecker>();
        var standing = await studentStatusChecker.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        if (standing?.StudentId == ownerStudentId)
        {
            return true;
        }

        return await HasEitherManagePermissionAsync(httpContext, permissionResolver, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> HasEitherManagePermissionAsync(HttpContext httpContext, IPermissionResolver permissionResolver, CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var sessionId = httpContext.User.GetSessionId();

        var internshipOutcome = await permissionResolver.CheckAsync(userId, sessionId, CareerPermissions.InternshipManage, cancellationToken).ConfigureAwait(false);
        if (internshipOutcome == PermissionCheckOutcome.Granted)
        {
            return true;
        }

        var driveOutcome = await permissionResolver.CheckAsync(userId, sessionId, CareerPermissions.DriveManage, cancellationToken).ConfigureAwait(false);
        return driveOutcome == PermissionCheckOutcome.Granted;
    }
}
