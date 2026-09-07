using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Modules.Academic.Application.Programs;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-1 (bundled foundation): Program create/read (requirement-spec.md §6, bundled - see <c>ProgramService</c>'s own remarks).</summary>
internal static class ProgramEndpoints
{
    public static void MapProgramEndpoints(this RouteGroupBuilder group)
    {
        var programs = group.MapGroup("/programs");

        programs.MapPost("/", async (CreateProgramRequest body, HttpContext httpContext, ProgramService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/academic/programs/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.ProgramManage);

        programs.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, ProgramService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
