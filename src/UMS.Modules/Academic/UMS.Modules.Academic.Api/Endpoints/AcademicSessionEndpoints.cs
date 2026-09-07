using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.AcademicSessions;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-3 (bundled foundation): AcademicSession/Semester create (requirement-spec.md §2, bundled - see <c>AcademicSessionService</c>'s own remarks).</summary>
internal static class AcademicSessionEndpoints
{
    public static void MapAcademicSessionEndpoints(this RouteGroupBuilder group)
    {
        var sessions = group.MapGroup("/academic-sessions");

        sessions.MapPost("/", async (CreateAcademicSessionRequest body, HttpContext httpContext, AcademicSessionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/academic/academic-sessions/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.AcademicSessionManage);

        sessions.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, AcademicSessionService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
