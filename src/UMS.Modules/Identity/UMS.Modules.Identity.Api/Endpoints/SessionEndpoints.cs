using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Identity.Application.Sessions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Identity.Api.Endpoints;

/// <summary>
/// IDN-9: Session introspection/single revoke, scoped to the caller's own Sessions
/// (requirement-spec.md identity §6). Gated by <c>RequireLiveSession</c> rather than a Role
/// Permission - see <see cref="UMS.Shared.Authorization.IPermissionResolver.CheckLivenessAsync"/>'s
/// own remarks for why self-service session management isn't a grantable Permission.
/// </summary>
internal static class SessionEndpoints
{
    public static void MapSessionEndpoints(this RouteGroupBuilder group)
    {
        var sessions = group.MapGroup("/sessions").RequireLiveSession();

        sessions.MapGet("/", async (HttpContext httpContext, SessionManagementService service, CancellationToken cancellationToken) =>
        {
            var currentSessionId = httpContext.User.GetSessionId();
            var result = await service.ListForUserAsync(httpContext.User.GetUserId(), currentSessionId, cancellationToken).ConfigureAwait(false);
            return Results.Ok(result);
        });

        sessions.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, SessionManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RevokeOwnedSessionAsync(httpContext.User.GetUserId(), id, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        });
    }
}
