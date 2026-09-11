using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Drives;
using UMS.Modules.Career.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.Student;

namespace UMS.Modules.Career.Api.Endpoints;

/// <summary>CAR-10/11/12/13/14/15: CampusRecruitmentDrive lifecycle, registration, InterviewSlot definition/booking/cancellation, shortlisting, and cancellation + cascade (requirement-spec.md §2.3, §6 `/career/drives`).</summary>
internal static class DriveEndpoints
{
    public static void MapDriveEndpoints(this RouteGroupBuilder group)
    {
        var drives = group.MapGroup("/drives");

        drives.MapGet("/", async (Guid? employerProfileId, bool? includeAllStatuses, int? skip, int? take, HttpContext httpContext, CampusRecruitmentDriveService service, IPermissionResolver permissionResolver, CancellationToken cancellationToken) =>
        {
            var wantsAllStatuses = includeAllStatuses ?? false;
            var effectiveIncludeAllStatuses = wantsAllStatuses
                && await permissionResolver.CheckAsync(httpContext.User.GetUserId(), httpContext.User.GetSessionId(), CareerPermissions.DriveManage, cancellationToken).ConfigureAwait(false) == PermissionCheckOutcome.Granted;

            return Results.Ok(await service.ListAsync(employerProfileId, effectiveIncludeAllStatuses, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false));
        }).RequireLiveSession();

        drives.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, CampusRecruitmentDriveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        drives.MapPost("/", async (CreateDriveRequest body, HttpContext httpContext, CampusRecruitmentDriveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/career/drives/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.DriveManage);

        drives.MapPatch("/{id:guid}", async (Guid id, EditDriveRequest body, HttpContext httpContext, CampusRecruitmentDriveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.EditAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.DriveManage);

        drives.MapPost("/{id:guid}/schedule", async (Guid id, uint version, HttpContext httpContext, CampusRecruitmentDriveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ScheduleAsync(id, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.DriveManage);

        drives.MapPost("/{id:guid}/open-registration", async (Guid id, uint version, HttpContext httpContext, CampusRecruitmentDriveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.OpenRegistrationAsync(id, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.DriveManage);

        drives.MapPost("/{id:guid}/close-registration", async (Guid id, uint version, HttpContext httpContext, CampusRecruitmentDriveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CloseRegistrationAsync(id, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.DriveManage);

        drives.MapPost("/{id:guid}/complete", async (Guid id, uint version, HttpContext httpContext, CampusRecruitmentDriveService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CompleteAsync(id, version, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.DriveManage);

        // CAR-15: identical cascade posture as the Internship's own withdrawal - the Drive's own cancellation commits first, then the fan-out (including releasing any booked InterviewSlot capacity) runs immediately after, in-process.
        drives.MapPost("/{id:guid}/cancel", async (Guid id, CancelDriveRequest body, HttpContext httpContext, CampusRecruitmentDriveService service, DriveCancellationCascadeHandler cascadeHandler, CancellationToken cancellationToken) =>
        {
            var result = await service.CancelAsync(id, body, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return result.Error!.ToProblemResult(httpContext);
            }

            await cascadeHandler.HandleAsync(id, httpContext.GetCorrelationId(), cancellationToken).ConfigureAwait(false);
            return Results.Ok(result.Value);
        }).RequirePermission(CareerPermissions.DriveManage);

        // CAR-11: a Student "registers" for a Drive by submitting a CareerApplication targeting drive_id - the identical guarded-insert mechanism as an Internship application.
        drives.MapPost("/{id:guid}/register", async (Guid id, RegisterForDriveRequest body, HttpContext httpContext, DriveApplicationService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterAsync(httpContext.User.GetUserId(), id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/career/applications/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        drives.MapGet("/{id:guid}/applications", async (Guid id, CareerApplicationReviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByDriveAsync(id, cancellationToken).ConfigureAwait(false))).RequirePermission(CareerPermissions.DriveManage);

        // CAR-11: bulk shortlisting - requirement-spec.md §9 "no automated shortlisting in v1", fully staff-driven.
        drives.MapPost("/{id:guid}/shortlist", async (Guid id, ShortlistDriveApplicationsRequest body, CareerApplicationReviewService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ShortlistAsync(body.CareerApplicationIds, cancellationToken).ConfigureAwait(false))).RequirePermission(CareerPermissions.DriveManage);

        // CAR-12: InterviewSlot definition (staff) and listing (any live-session caller needs to see what's bookable).
        drives.MapPost("/{id:guid}/slots", async (Guid id, DefineInterviewSlotsRequest body, HttpContext httpContext, InterviewSlotService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DefineSlotsAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(CareerPermissions.DriveManage);

        drives.MapGet("/{id:guid}/slots", async (Guid id, InterviewSlotService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListByDriveAsync(id, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        // CAR-12: the module's highest-concurrency write path - the atomic conditional-write booking.
        drives.MapPost("/{id:guid}/slots/{slotId:guid}/book", async (Guid id, Guid slotId, BookInterviewSlotRequest body, HttpContext httpContext, InterviewSlotService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.BookAsync(id, slotId, body.CareerApplicationId, studentId.Value, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        // CAR-13: the symmetric atomic capacity release - no waitlist mechanism in v1.
        drives.MapPost("/{id:guid}/slots/{slotId:guid}/cancel", async (Guid id, Guid slotId, CancelInterviewSlotBookingRequest body, HttpContext httpContext, InterviewSlotService service, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken) =>
        {
            var studentId = await studentStatusChecker.ResolveStudentIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            if (studentId.IsFailure)
            {
                return studentId.Error!.ToProblemResult(httpContext);
            }

            var result = await service.CancelBookingAsync(id, slotId, body.CareerApplicationId, studentId.Value, cancellationToken).ConfigureAwait(false);
            return result.IsFailure ? result.Error!.ToProblemResult(httpContext) : Results.Ok();
        }).RequireLiveSession();
    }
}
