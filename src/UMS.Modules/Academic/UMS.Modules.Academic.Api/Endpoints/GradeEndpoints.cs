using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Grades;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-10/ACD-13: Faculty grade submission, Registrar-approved correction workflow (requirement-spec.md §6 <c>POST /grades</c>, <c>POST /grades/{id}/correct</c>).</summary>
internal static class GradeEndpoints
{
    public static void MapGradeEndpoints(this RouteGroupBuilder group)
    {
        var grades = group.MapGroup("/grades");

        grades.MapPost("/", async (SubmitGradeRequest body, HttpContext httpContext, GradeService service, CancellationToken cancellationToken) =>
        {
            var result = await service.SubmitAsync(httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.GradeEnter);

        grades.MapPost("/{id:guid}/correct", async (Guid id, CorrectGradeRequest body, HttpContext httpContext, GradeCorrectionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CorrectAsync(id, httpContext.User.GetUserId(), body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.GradeCorrect);
    }
}
