using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Identity.Application;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Application.Auth;
using UMS.Modules.Identity.Application.Permissions;
using UMS.Modules.Identity.Application.Roles;
using UMS.Modules.Identity.Application.Sessions;
using UMS.Modules.Identity.Application.Users;
using UMS.Modules.Identity.Infrastructure.Authorization;
using UMS.Modules.Identity.Infrastructure.Caching;
using UMS.Modules.Identity.Infrastructure.Organization;
using UMS.Modules.Identity.Infrastructure.Persistence;
using UMS.Modules.Identity.Infrastructure.Persistence.Repositories;
using UMS.Modules.Identity.Infrastructure.Security;
using UMS.Shared.Authorization;

namespace UMS.Modules.Identity.Infrastructure;

/// <summary>
/// Composition root for the Identity module - the Host project calls
/// <see cref="AddIdentityModule"/> once, wiring every Application-layer abstraction to its one
/// Infrastructure implementation. No other module or the Host itself ever references
/// <c>UMS.Modules.Identity.Domain</c>/<c>.Infrastructure</c> directly (module-boundaries.md).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<IdentityDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IPermissionCatalogRepository, PermissionCatalogRepository>();

        services.AddSingleton<IClock, SystemClock>();

        // release/DEVELOPMENT_PLAN.md Flow #6 (Organization) now exists - resolves the real
        // cross-module existence check via UMS.Shared.Organization's shared interface. Scoped
        // (not Singleton, unlike most of this method's other registrations) because
        // OrganizationNodeExistenceCheckerAdapter's own dependency
        // (UMS.Shared.Organization.IOrganizationNodeExistenceChecker) is itself Scoped, bound to a
        // per-request OrganizationDbContext - see that registration's own remarks in
        // UMS.Modules.Organization.Infrastructure.DependencyInjection.
        services.AddScoped<IOrganizationNodeExistenceChecker, OrganizationNodeExistenceCheckerAdapter>();

        services.Configure<Argon2idOptions>(configuration.GetSection("Identity:Argon2"));
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();

        services.Configure<IdentityTokenOptions>(configuration.GetSection("Identity:Tokens"));
        services.AddSingleton<ITokenService, JwtTokenService>();

        // Shares the platform's one Redis connection (UMS.Shared.Resilience.AddUmsResilience),
        // never a second multiplexer (ADR-0007).
        services.AddScoped<IAuthzCache, RedisAuthzCache>();
        services.AddScoped<IPermissionResolver, IdentityPermissionResolver>();

        services.AddSingleton<IPermissionManifest, IdentityPermissionManifest>();

        services.AddScoped<UserProvisioningService>();
        services.AddScoped<UserQueryService>();
        services.AddScoped<UserStatusService>();
        services.AddScoped<RoleManagementService>();
        services.AddScoped<RoleAssignmentService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<TokenRefreshService>();
        services.AddScoped<SessionManagementService>();
        services.AddScoped<PermissionCatalogService>();

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations for the <c>identity</c> schema and synchronizes the
    /// Permission catalog from every registered <see cref="IPermissionManifest"/> - called once
    /// from the Host composition root after <c>WebApplication.Build()</c>.
    /// </summary>
    public static async Task UseIdentityModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        var catalogService = scope.ServiceProvider.GetRequiredService<PermissionCatalogService>();
        await catalogService.SynchronizeAsync().ConfigureAwait(false);
    }
}
