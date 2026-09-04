using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>ORG-2: Campus CRUD (requirement-spec.md organization §6).</summary>
internal static class CampusEndpoints
{
    public static void MapCampusEndpoints(this RouteGroupBuilder group)
    {
        var campuses = group.MapGroup("/campuses");

        campuses.MapGet("/", async (Guid? universityId, int? skip, int? take, CampusService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(universityId, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        campuses.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, CampusService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        campuses.MapPost("/", async (CreateCampusRequest body, HttpContext httpContext, CampusService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/campuses/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.CampusManage);

        campuses.MapPatch("/{id:guid}", async (Guid id, UpdateCampusRequest body, HttpContext httpContext, CampusService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdateAsync(id, body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.CampusManage);
    }
}
