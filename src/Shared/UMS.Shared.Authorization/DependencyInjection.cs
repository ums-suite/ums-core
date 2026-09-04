using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace UMS.Shared.Authorization;

/// <summary>
/// One shared registration for JWT authentication + permission-based authorization
/// (ums-conventions.md: "one shared implementation, not per-module reinvention"). Every module
/// that exposes a protected endpoint calls <see cref="RequirePermission(RouteHandlerBuilder, string)"/>
/// instead of hand-rolling its own <c>[Authorize]</c> policy.
/// </summary>
public static class DependencyInjection
{
    public const string LiveSessionPolicyName = "live-session";

    public static IServiceCollection AddUmsAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Read Jwt:* lazily, inside this callback, rather than eagerly in this method's
                // own body: ASP.NET Core only evaluates this callback when JwtBearerOptions is
                // first actually built (on the first authentication attempt) - by which point
                // every configuration source (including a test host's own overrides, added via
                // WebApplicationFactory.ConfigureWebHost) is guaranteed to be fully merged. An
                // eager read here previously raced that merge and could capture a stale value
                // (e.g. the Development appsettings default) while JwtTokenService's own lazy
                // read picked up the correct, later-merged one - a signing-key mismatch between
                // issuance and validation that manifested as every valid token being rejected.
                var jwtSection = configuration.GetSection("Jwt");
                var signingKey = jwtSection["SigningKey"]
                    ?? throw new InvalidOperationException("Missing required 'Jwt:SigningKey' configuration value.");
                var issuer = jwtSection["Issuer"]
                    ?? throw new InvalidOperationException("Missing required 'Jwt:Issuer' configuration value.");
                var audience = jwtSection["Audience"]
                    ?? throw new InvalidOperationException("Missing required 'Jwt:Audience' configuration value.");

                // Keep claim types exactly as Identity's token issuer wrote them ("sub", "sid",
                // "roles") - disables the legacy WS-Federation claim-URI remapping ASP.NET Core
                // applies by default, which UmsClaimTypes assumes is off.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        return services;
    }

    public static IServiceCollection AddUmsAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationPolicyProvider, DynamicPermissionPolicyProvider>();

        // Scoped, not Singleton - both handlers depend on IPermissionResolver, which Identity's
        // implementation resolves against a scoped DbContext (module-boundaries.md: the
        // Infrastructure implementation, not this shared package, owns that lifetime choice).
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, LiveSessionAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(LiveSessionPolicyName, policy => policy.RequireAuthenticatedUser().AddRequirements(new LiveSessionRequirement()));
        return services;
    }

    /// <summary>Gates a minimal-API endpoint behind a single Permission string (identity §2's catalog convention).</summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string permission) =>
        builder.RequireAuthorization(DynamicPermissionPolicyProvider.PermissionPolicyPrefix + permission);

    /// <summary>Gates every endpoint in a minimal-API route group behind a single Permission string.</summary>
    public static RouteGroupBuilder RequirePermission(this RouteGroupBuilder builder, string permission) =>
        builder.RequireAuthorization(DynamicPermissionPolicyProvider.PermissionPolicyPrefix + permission);

    /// <summary>Gates a minimal-API endpoint behind "authenticated, active User, non-revoked Session" only - see <see cref="LiveSessionRequirement"/>.</summary>
    public static RouteHandlerBuilder RequireLiveSession(this RouteHandlerBuilder builder) =>
        builder.RequireAuthorization(LiveSessionPolicyName);

    /// <summary>Gates every endpoint in a minimal-API route group behind "authenticated, active User, non-revoked Session" only.</summary>
    public static RouteGroupBuilder RequireLiveSession(this RouteGroupBuilder builder) =>
        builder.RequireAuthorization(LiveSessionPolicyName);
}
