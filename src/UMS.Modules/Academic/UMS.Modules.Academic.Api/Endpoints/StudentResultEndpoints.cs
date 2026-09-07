using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Modules.Academic.Application.ResultPublications;
using UMS.Shared.Authorization;
using UMS.Shared.Student;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>
/// ACD-14/ACD-15: a Student's results/Transcript - Published grades only (requirement-spec.md §6
/// <c>GET /students/{id}/results</c> / <c>.../transcript</c>, permission "`student.result.read` or
/// Student-owned"). Since that's an OR between a named permission and self-ownership, it can't be
/// expressed as one static <c>RequirePermission</c> policy - resolved as an ad-hoc check per
/// request instead, mirroring Faculty's own <c>PermissionResolverExtensions</c> pattern.
/// </summary>
internal static class StudentResultEndpoints
{
    public static void MapStudentResultEndpoints(this RouteGroupBuilder group)
    {
        var students = group.MapGroup("/students/{id:guid}");

        students.MapGet("/results", async (Guid id, HttpContext httpContext, IPermissionResolver permissionResolver, IStudentStatusChecker studentStatusChecker, StudentResultQueryService service, CancellationToken cancellationToken) =>
        {
            if (!await IsAuthorizedAsync(id, httpContext, permissionResolver, studentStatusChecker, cancellationToken).ConfigureAwait(false))
            {
                return Results.Forbid();
            }

            var results = await service.GetPublishedResultsAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Ok(results);
        }).RequireLiveSession();

        students.MapGet("/transcript", async (Guid id, HttpContext httpContext, IPermissionResolver permissionResolver, IStudentStatusChecker studentStatusChecker, StudentResultQueryService service, CancellationToken cancellationToken) =>
        {
            if (!await IsAuthorizedAsync(id, httpContext, permissionResolver, studentStatusChecker, cancellationToken).ConfigureAwait(false))
            {
                return Results.Forbid();
            }

            var transcript = await service.GetTranscriptAsync(id, cancellationToken).ConfigureAwait(false);
            return Results.Ok(transcript);
        }).RequireLiveSession();
    }

    private static async Task<bool> IsAuthorizedAsync(Guid studentId, HttpContext httpContext, IPermissionResolver permissionResolver, IStudentStatusChecker studentStatusChecker, CancellationToken cancellationToken)
    {
        if (await permissionResolver.HasPermissionAsync(httpContext.User, AcademicPermissions.StudentResultRead, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        var callerStanding = await studentStatusChecker.GetByUserIdAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
        return callerStanding?.StudentId == studentId;
    }
}
