using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Student.Application.Permissions;
using UMS.Modules.Student.Application.Students;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Student.Api.Endpoints;

/// <summary>STU-5/STU-6/STU-7 (requirement-spec.md student §6).</summary>
internal static class StudentProfileEndpoints
{
    public static void MapStudentProfileEndpoints(this RouteGroupBuilder group)
    {
        var students = group.MapGroup("/students");

        students.MapGet("/me", async (HttpContext httpContext, StudentProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetOwnProfileAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        students.MapPut("/me", async (UpdateSelfServiceProfileRequest body, HttpContext httpContext, StudentProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateOwnProfileAsync(httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();

        students.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, StudentProfileService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(StudentPermissions.ProfileRead);
    }
}
