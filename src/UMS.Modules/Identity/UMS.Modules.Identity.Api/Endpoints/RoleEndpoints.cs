using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Identity.Application.Permissions;
using UMS.Modules.Identity.Application.Roles;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Identity.Api.Endpoints;

/// <summary>IDN-14/IDN-15: Role/Permission-bundle management and Role assignment (requirement-spec.md identity §6).</summary>
internal static class RoleEndpoints
{
    public static void MapRoleEndpoints(this RouteGroupBuilder group)
    {
        var roles = group.MapGroup("/roles").RequirePermission(IdentityPermissions.RoleManage);

        roles.MapGet("/", async (RoleManagementService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken).ConfigureAwait(false)));

        roles.MapPost("/", async (CreateRoleRequest body, HttpContext httpContext, RoleManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.CreateAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/identity/roles/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        });

        roles.MapPatch("/{id:guid}/permissions", async (Guid id, UpdateRolePermissionsRequest body, HttpContext httpContext, RoleManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.UpdatePermissionsAsync(id, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        });

        // Role *assignment* (binding a Role to a specific User, optionally ScopeGrant-bound) is a
        // distinct permission from Role *management* (identity §2/§6) - assigning who holds a Role
        // is a materially different, more sensitive capability than defining the Role itself.
        group.MapPost("/users/{userId:guid}/roles", async (Guid userId, AssignRoleRequest body, HttpContext httpContext, RoleAssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.AssignAsync(userId, body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(IdentityPermissions.RoleAssign);

        group.MapDelete("/users/{userId:guid}/roles/{assignmentId:guid}", async (Guid userId, Guid assignmentId, HttpContext httpContext, RoleAssignmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RevokeAsync(userId, assignmentId, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        }).RequirePermission(IdentityPermissions.RoleAssign);
    }
}
