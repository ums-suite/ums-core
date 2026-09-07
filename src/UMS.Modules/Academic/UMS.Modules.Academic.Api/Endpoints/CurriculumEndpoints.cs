using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Academic.Application.Curricula;
using UMS.Modules.Academic.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Academic.Api.Endpoints;

/// <summary>ACD-1: Curriculum create/read (requirement-spec.md §6 <c>POST /curriculums</c>).</summary>
internal static class CurriculumEndpoints
{
    public static void MapCurriculumEndpoints(this RouteGroupBuilder group)
    {
        var curricula = group.MapGroup("/curriculums");

        curricula.MapPost("/", async (CreateCurriculumRequest body, HttpContext httpContext, CurriculumService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(dto => Results.Created($"/api/v1/academic/curriculums/{dto.Id}", dto), error => error.ToProblemResult(httpContext));
        }).RequirePermission(AcademicPermissions.CurriculumManage);

        curricula.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, CurriculumService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequireLiveSession();
    }
}
