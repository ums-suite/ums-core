using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Organization.Application.Facilities;
using UMS.Modules.Organization.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Api.Endpoints;

/// <summary>
/// ORG-7: Building &amp; Room CRUD (requirement-spec.md organization §6). `DELETE /buildings/{id}`
/// and `DELETE /rooms/{id}` extend §6's literal table - see
/// <c>UMS.Modules.Organization.Application.Facilities.BuildingService</c>'s own remarks on why:
/// design-decisions.md's "Soft-Delete/Deactivate-Only Pattern" explicitly re-authorizes hard
/// delete for Room/Building specifically, and this build's own brief directs implementing it with
/// the re-check-before-commit pattern - tickets.md's Flagged Gaps blanket "no delete verb" note is
/// read here as covering only the University→Program deactivate-only chain, not this
/// separately-decided exception.
/// </summary>
internal static class FacilityEndpoints
{
    public static void MapFacilityEndpoints(this RouteGroupBuilder group)
    {
        var buildings = group.MapGroup("/buildings");

        buildings.MapGet("/", async (Guid? campusId, int? skip, int? take, BuildingService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(campusId, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        buildings.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, BuildingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        buildings.MapPost("/", async (CreateBuildingRequest body, HttpContext httpContext, BuildingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/buildings/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.FacilityManage);

        buildings.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, BuildingService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(OrganizationPermissions.FacilityManage);

        buildings.MapGet("/{id:guid}/rooms", async (Guid id, int? skip, int? take, RoomService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListByBuildingAsync(id, skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).AllowAnonymous();

        var rooms = group.MapGroup("/rooms");

        rooms.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, RoomService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        rooms.MapPost("/", async (CreateRoomRequest body, HttpContext httpContext, RoomService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/organization/rooms/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).RequirePermission(OrganizationPermissions.FacilityManage);

        rooms.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, RoomService service, CancellationToken cancellationToken) =>
        {
            var result = await service.DeleteAsync(id, httpContext.GetAuditContext(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(OrganizationPermissions.FacilityManage);
    }
}
