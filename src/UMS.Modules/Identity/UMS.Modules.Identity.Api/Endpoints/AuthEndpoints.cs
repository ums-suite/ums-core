using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using UMS.Modules.Identity.Api.Contracts;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Application.Sessions;
using UMS.Shared.Authorization;
using UMS.Shared.ErrorHandling;

namespace UMS.Modules.Identity.Api.Endpoints;

/// <summary>
/// IDN-5/IDN-6/IDN-7/IDN-8/IDN-10/IDN-11/IDN-12: login, refresh, logout, "log out everywhere",
/// TOTP MFA enrollment/verification, password forgot/reset (requirement-spec.md identity §6). The
/// four rate-limiting policy names below are registered once, at the Host composition root
/// (mirrors Documents' own `document-verify` policy) - identity §2/§5's "rate limiting on the
/// login and OTP endpoints specifically."
/// </summary>
internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder group)
    {
        var auth = group.MapGroup("/auth");

        auth.MapPost("/login", async (LoginRequestBody body, HttpContext httpContext, AuthenticationService service, CancellationToken cancellationToken) =>
        {
            var request = new LoginRequest(body.Identifier, body.Password, UserAgentOf(httpContext), RemoteIpOf(httpContext));
            var result = await service.LoginAsync(request, cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(
                outcome => outcome switch
                {
                    LoginSucceeded succeeded => TypedResults.Ok(succeeded.Tokens),
                    LoginRequiresMfa requiresMfa => TypedResults.Ok(requiresMfa),
                    _ => throw new InvalidOperationException($"Unhandled {nameof(LoginOutcome)}: {outcome.GetType()}"),
                },
                error => error.ToProblemResult(httpContext));
        }).RequireRateLimiting("identity-login");

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

        // IDN-10/IDN-11: any authenticated principal, live session OR a mid-login MFA-challenge
        // token (RequireLiveSession() would reject the latter - no real Session exists yet) - see
        // ITokenService.IssueMfaChallengeToken's own remarks. Self-service on one's own MFA state
        // needs no Permission, only proof of identity.
        auth.MapPost("/mfa/enroll", async (HttpContext httpContext, MfaEnrollmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.EnrollAsync(httpContext.User.GetUserId(), cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(TypedResults.Ok, error => error.ToProblemResult(httpContext));
        }).RequireAuthorization();

        auth.MapPost("/mfa/verify", async (MfaVerifyRequestBody body, HttpContext httpContext, MfaEnrollmentService service, CancellationToken cancellationToken) =>
        {
            var result = await service.VerifyAsync(
                httpContext.User.GetUserId(),
                body.Code,
                httpContext.User.IsMfaChallenge(),
                UserAgentOf(httpContext),
                RemoteIpOf(httpContext),
                cancellationToken).ConfigureAwait(false);
            return result.Match<IResult>(TypedResults.Ok, error => error.ToProblemResult(httpContext));
        }).RequireAuthorization().RequireRateLimiting("identity-mfa-verify");

        auth.MapPost("/password/forgot", async (ForgotPasswordRequestBody body, PasswordResetService service, CancellationToken cancellationToken) =>
        {
            await service.ForgotAsync(body.Identifier, cancellationToken).ConfigureAwait(false);

            // requirement-spec.md identity §5 Security NFR's generic-failure posture - the response
            // never confirms or denies that the identifier resolved to a real User.
            return Results.Accepted();
        }).AllowAnonymous().RequireRateLimiting("identity-password-forgot");

        auth.MapPost("/password/reset", async (ResetPasswordRequestBody body, HttpContext httpContext, PasswordResetService service, CancellationToken cancellationToken) =>
        {
            var result = await service.ResetAsync(body.Token, body.NewPassword, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? Results.NoContent() : result.Error!.ToProblemResult(httpContext);
        }).AllowAnonymous().RequireRateLimiting("identity-password-reset");
    }

    private static string? UserAgentOf(HttpContext httpContext) =>
        httpContext.Request.Headers.UserAgent.Count > 0 ? httpContext.Request.Headers.UserAgent.ToString() : null;

    private static string? RemoteIpOf(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString();
}
