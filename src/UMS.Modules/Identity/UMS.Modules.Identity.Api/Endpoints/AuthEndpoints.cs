using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Identity.Api.Contracts;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Application.Sessions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Identity.Api.Endpoints;

/// <summary>IDN-5/IDN-6/IDN-7/IDN-8: login, refresh, logout, "log out everywhere" (requirement-spec.md identity §6).</summary>
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth");

        auth.MapPost("/login", async (LoginRequestBody body, HttpContext httpContext, AuthenticationService service, CancellationToken cancellationToken) =>
        {
            var request = new LoginRequest(body.Identifier, body.Password, UserAgentOf(httpContext), RemoteIpOf(httpContext));
            var result = await service.LoginAsync(request, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(TypedResults.Ok, error => error.ToProblemResult(httpContext));
        });

        auth.MapPost("/refresh", async (RefreshRequestBody body, HttpContext httpContext, TokenRefreshService service, CancellationToken cancellationToken) =>
        {
            var request = new RefreshRequest(body.RefreshToken, UserAgentOf(httpContext), RemoteIpOf(httpContext));
            var result = await service.RefreshAsync(request, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(TypedResults.Ok, error => error.ToProblemResult(httpContext));
        });

        auth.MapPost("/logout", async (HttpContext httpContext, SessionManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.LogoutCurrentAsync(httpContext.User.GetSessionId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        }).RequireLiveSession();

        auth.MapPost("/logout-all", async (HttpContext httpContext, SessionManagementService service, CancellationToken cancellationToken) =>
        {
            var result = await service.LogoutAllAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        }).RequireLiveSession();
    }

    private static string? UserAgentOf(HttpContext httpContext) =>
        httpContext.Request.Headers.UserAgent.Count > 0 ? httpContext.Request.Headers.UserAgent.ToString() : null;

    private static string? RemoteIpOf(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();
}
