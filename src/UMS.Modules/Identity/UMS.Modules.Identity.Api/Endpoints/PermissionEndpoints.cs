using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Identity.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Identity.Api.Endpoints;

/// <summary>IDN-1: `GET /permissions` - the registered catalog, for admin UI role-builder screens (requirement-spec.md identity §6).</summary>
internal static class PermissionEndpoints
{
    public static void MapPermissionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/permissions", async (PermissionCatalogService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAllAsync(cancellationToken).ConfigureAwait(false)))
            .RequirePermission(IdentityPermissions.PermissionRead);
    }
}
