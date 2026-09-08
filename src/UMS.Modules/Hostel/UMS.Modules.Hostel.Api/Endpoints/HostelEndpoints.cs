using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Hostel.Application.Hostels;
using UMS.Modules.Hostel.Application.Permissions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Api.Endpoints;

/// <summary>HOS-1: requirement-spec.md §2 Inventory Management, §6 <c>GET /api/v1/hostel/hostels</c>.</summary>
internal static class HostelEndpoints
{
    public static void MapHostelEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/hostels", async (InventoryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetCatalogAsync(cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        group.MapPost("/hostels", async (CreateHostelRequest body, HttpContext httpContext, InventoryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateHostelAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.InventoryManage);

        group.MapGet("/hostels/{hostelId:guid}/buildings", async (Guid hostelId, InventoryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetBuildingsAsync(hostelId, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        group.MapPost("/hostels/{hostelId:guid}/buildings", async (Guid hostelId, CreateBuildingHttpRequest body, HttpContext httpContext, InventoryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateBuildingAsync(new CreateBuildingRequest(hostelId, body.Name), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.InventoryManage);

        group.MapGet("/buildings/{buildingId:guid}/rooms", async (Guid buildingId, InventoryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetRoomsAsync(buildingId, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        group.MapPost("/buildings/{buildingId:guid}/rooms", async (Guid buildingId, CreateRoomHttpRequest body, HttpContext httpContext, InventoryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateRoomAsync(new CreateRoomRequest(buildingId, body.RoomNumber, body.Type, body.Capacity), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.InventoryManage);

        group.MapPatch("/rooms/{roomId:guid}/capacity", async (Guid roomId, ChangeRoomCapacityRequest body, HttpContext httpContext, InventoryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ChangeRoomCapacityAsync(roomId, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.InventoryManage);

        group.MapGet("/rooms/{roomId:guid}/beds", async (Guid roomId, InventoryService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetBedsAsync(roomId, cancellationToken).ConfigureAwait(false))).RequireLiveSession();

        group.MapPost("/rooms/{roomId:guid}/beds", async (Guid roomId, CreateBedHttpRequest body, HttpContext httpContext, InventoryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateBedAsync(new CreateBedRequest(roomId, body.Label), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(HostelPermissions.InventoryManage);
    }

    private sealed record CreateBuildingHttpRequest(string Name);

    private sealed record CreateRoomHttpRequest(string RoomNumber, string Type, int Capacity);

    private sealed record CreateBedHttpRequest(string Label);
}
