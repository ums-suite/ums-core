using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Identity.Api.Contracts;
using UMS.Modules.Identity.Application.Permissions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Identity.Api.Endpoints;

/// <summary>IDN-2/IDN-3/IDN-13: User provisioning, lookup/listing, suspend/reactivate (requirement-spec.md identity §6).</summary>
internal static class UserEndpoints
{
    public static void MapUserEndpoints(this RouteGroupBuilder group)
    {
        var users = group.MapGroup("/users");

        // Serves both self-registration and another module's confirmation flow (identity §6);
        // admin-initiated creation with elevated fields is not yet split into its own gated path
        // (requirement-spec.md identity §6's single `POST /users` row covers both today).
        users.MapPost("/", async (ProvisionUserRequest body, HttpContext httpContext, UserProvisioningService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ProvisionAsync(body, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                dto => Results.Created($"/api/v1/identity/users/{dto.Id}", dto),
                error => error.ToProblemResult(httpContext));
        }).AllowAnonymous();

        users.MapGet("/{id:guid}", async (Guid id, HttpContext httpContext, UserQueryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(IdentityPermissions.UserRead);

        users.MapGet("/", async (int? skip, int? take, UserQueryService service, CancellationToken cancellationToken) =>
        {
            var page = await service.ListAsync(skip ?? 0, take ?? 50, cancellationToken).ConfigureAwait(false);
            return Results.Ok(page);
        }).RequirePermission(IdentityPermissions.UserRead);

        users.MapPatch("/{id:guid}/status", async (Guid id, ChangeUserStatusRequestBody body, HttpContext httpContext, UserStatusService service, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<UserStatus>(body.Status, ignoreCase: true, out var targetStatus))
            {
                return Error.Validation("user.invalid_status", "Status must be 'Active' or 'Suspended'.").ToProblemResult(httpContext);
            }

            var result = await service.ChangeStatusAsync(id, targetStatus, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(Results.Ok, error => error.ToProblemResult(httpContext));
        }).RequirePermission(IdentityPermissions.UserManage);
    }
}
