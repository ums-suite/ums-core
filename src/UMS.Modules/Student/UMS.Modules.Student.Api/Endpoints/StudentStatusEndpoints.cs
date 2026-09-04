using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Student.Application.Permissions;
using UMS.Modules.Student.Application.Students;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Student.Api.Endpoints;

/// <summary>
/// STU-8 (requirement-spec.md student §6 <c>POST /students/{id}/status</c>).
///
/// <para>
/// design-decisions.md, "Status-Change Transactional Boundary" (edge-cases.md's "reject and show
/// the current state, don't blind-retry" refinement): on a version conflict, this endpoint - not
/// <c>StudentStatusService</c> itself - re-fetches and attaches the Student's actual current state
/// to the <c>409</c> response, so the losing caller can see exactly what happened without a second
/// round trip. This is a small, deliberate, documented deviation from the platform's plain
/// <c>Result</c>/<c>Error</c> envelope for this one endpoint (see <c>StudentStatusService</c>'s own
/// remarks) rather than widening that shared shape platform-wide for a single call site.
/// </para>
/// </summary>
internal static class StudentStatusEndpoints
{
    public static void MapStudentStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/students/{id:guid}/status", async (Guid id, ChangeStudentStatusRequest body, HttpContext httpContext, StudentStatusService statusService, StudentProfileService profileService, CancellationToken cancellationToken) =>
        {
            var result = await statusService.ChangeStatusAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                return Results.Ok(result.Value);
            }

            if (result.Error!.Code == "student.version_conflict")
            {
                var current = await profileService.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
                if (current.IsSuccess)
                {
                    return Results.Conflict(new StudentStatusConflict(current.Value));
                }
            }

            return result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(StudentPermissions.StatusChange);
    }
}
