using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Attendance;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-9: per-session attendance recording (requirement-spec.md §6 <c>POST /attendance</c>, Instructor via a fresh Faculty lookup).</summary>
internal static class AttendanceEndpoints
{
    public static void MapAttendanceEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/attendance", async (MarkAttendanceRequest body, HttpContext httpContext, AttendanceService service, CancellationToken cancellationToken) =>
        {
            var result = await service.MarkAsync(httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.AttendanceRecord);
    }
}
