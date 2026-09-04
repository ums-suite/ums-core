using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Modules.Organization.Application.Universities;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>ORG-1: University CRUD (requirement-spec.md organization §6). Reads are `AllowAnonymous` - see <see cref="OrganizationPermissions"/>'s own remarks on why.</summary>
internal static class UniversityEndpoints
{
    public static void MapUniversityEndpoints(this RouteGroupBuilder group)
    {
        var universities = group.MapGroup("/universities");

        universities.MapGet("/", async (int? skip, int? take, UniversityService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        universities.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, UniversityService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        universities.MapPost("/", async (CreateUniversityRequest body, HttpContext httpContext, UniversityService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/universities/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.UniversityManage);

        universities.MapPatch("/{id:guid}", async (Guid id, UpdateUniversityRequest body, HttpContext httpContext, UniversityService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.UniversityManage);
    }
}
