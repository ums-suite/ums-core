using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Application.Campuses;
using UMS.Modules.Organization.Application.Departments;
using UMS.Modules.Organization.Application.Designations;
using UMS.Modules.Organization.Application.Facilities;
using UMS.Modules.Organization.Application.Faculties;
using UMS.Modules.Organization.Application.Hierarchy;
using UMS.Modules.Organization.Application.Programs;
using UMS.Modules.Organization.Application.Universities;
using UMS.Modules.Organization.Infrastructure.Authorization;
using UMS.Modules.Organization.Infrastructure.Caching;
using UMS.Modules.Organization.Infrastructure.CrossModule;
using UMS.Modules.Organization.Infrastructure.Persistence;
using UMS.Modules.Organization.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;
using UMS.Shared.Organization;

namespace UMS.Modules.Organization.Infrastructure;

/// <summary>
/// Composition root for the Organization module - mirrors Identity's own <c>DependencyInjection</c>
/// exactly (release/DEVELOPMENT_PLAN.md Flow #6). The Host project calls <see cref="AddOrganizationModule"/>
/// once, wiring every Application-layer abstraction to its one Infrastructure implementation. No
/// other module or the Host itself ever references <c>UMS.Modules.Organization.Domain</c>/
/// <c>.Infrastructure</c> directly (module-boundaries.md) - other modules that need to ask
/// Organization something (today, only Identity) go through <c>UMS.Shared.Organization</c>'s
/// public interface instead, the same way every module reaches Audit through
/// <c>UMS.Shared.Audit</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddOrganizationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrganizationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organization")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<OrganizationDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<OrganizationDbContext>());

        services.AddScoped<IUniversityRepository, UniversityRepository>();
        services.AddScoped<ICampusRepository, CampusRepository>();
        services.AddScoped<IFacultyRepository, FacultyRepository>();
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<IDesignationRepository, DesignationRepository>();
        services.AddScoped<IBuildingRepository, BuildingRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();

        services.AddSingleton<IClock, SystemClock>();

        // Faculty (Flow #10) now exists - the former StubFacultyEmploymentChecker is replaced by a
        // real adapter onto UMS.Shared.Faculty.IFacultyEmploymentChecker (see
        // FacultyEmploymentCheckerAdapter's own remarks).
        services.AddScoped<IFacultyEmploymentChecker, FacultyEmploymentCheckerAdapter>();

        // EXPLICIT SEAM - Hostel (Flow #19) doesn't exist yet. See the stub's own remarks.
        services.AddScoped<IRoomReferenceChecker, StubRoomReferenceChecker>();

        // The REAL cross-module implementation Identity's own former stub
        // (StubOrganizationNodeExistenceChecker) is replaced by, now that Organization exists -
        // registered against the shared interface so Identity's Infrastructure can resolve it
        // in-process without depending on this assembly (module-boundaries.md, ADR-0002).
        services.AddScoped<IOrganizationNodeExistenceChecker, OrganizationNodeExistenceChecker>();

        // Shares the platform's one Redis connection (UMS.Shared.Resilience.AddUmsResilience), never a second multiplexer (ADR-0007).
        services.AddScoped<IOrganizationTreeCache, RedisOrganizationTreeCache>();

        services.AddSingleton<IPermissionManifest, OrganizationPermissionManifest>();

        services.AddScoped<HierarchyAncestryResolver>();

        services.AddScoped<UniversityService>();
        services.AddScoped<CampusService>();
        services.AddScoped<FacultyService>();
        services.AddScoped<DepartmentService>();
        services.AddScoped<ProgramService>();
        services.AddScoped<DesignationService>();
        services.AddScoped<BuildingService>();
        services.AddScoped<RoomService>();
        services.AddScoped<HierarchyQueryService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>organization</c> schema - called once from the Host composition root, mirroring Identity's own <c>UseIdentityModuleAsync</c>. Organization's own Permission manifest is picked up automatically by Identity's already-existing catalog sync (<c>PermissionCatalogService.SynchronizeAsync</c>) - no separate call is needed here.</summary>
    public static async Task UseOrganizationModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<OrganizationDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
