using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Application.CourseAssignments;
using UMS.Modules.Faculty.Application.FacultyMembers;
using UMS.Modules.Faculty.Application.LeaveRequests;
using UMS.Modules.Faculty.Application.ResearchProfiles;
using UMS.Modules.Faculty.Infrastructure.Authorization;
using UMS.Modules.Faculty.Infrastructure.CrossModule;
using UMS.Modules.Faculty.Infrastructure.Persistence;
using UMS.Modules.Faculty.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;
using UMS.Shared.Faculty;

namespace UMS.Modules.Faculty.Infrastructure;

/// <summary>
/// Composition root for the Faculty module (release/DEVELOPMENT_PLAN.md Flow #10) - mirrors
/// Organization's own <c>DependencyInjection</c> exactly. No other module or the Host itself ever
/// references <c>UMS.Modules.Faculty.Domain</c>/<c>.Infrastructure</c> directly
/// (module-boundaries.md) - other modules go through <c>UMS.Shared.Faculty</c> instead, the same
/// way every module reaches Audit through <c>UMS.Shared.Audit</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddFacultyModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FacultyDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "faculty")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FacultyDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<FacultyDbContext>());

        services.AddScoped<IFacultyMemberRepository, FacultyMemberRepository>();
        services.AddScoped<ICourseAssignmentRepository, CourseAssignmentRepository>();
        services.AddScoped<ILeaveRequestRepository, LeaveRequestRepository>();
        services.AddScoped<IResearchProfileRepository, ResearchProfileRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();
        services.AddScoped<IInstructorAssignmentEventSource, AcademicOutboxEventSource>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IOrganizationDepartmentExistenceChecker, OrganizationDepartmentExistenceCheckerAdapter>();
        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();

        // The REAL cross-module implementations - Organization's own former
        // StubFacultyEmploymentChecker registration is replaced by an adapter onto this shared
        // interface (see Organization.Infrastructure's own DependencyInjection.cs and
        // FacultyEmploymentCheckerAdapter).
        services.AddScoped<IFacultyEmploymentChecker, FacultyEmploymentChecker>();
        services.AddScoped<IFacultyMemberLookup, FacultyMemberLookup>();

        services.AddSingleton<IPermissionManifest, FacultyPermissionManifest>();

        services.AddScoped<FacultyMemberService>();
        services.AddScoped<CourseAssignmentQueryService>();
        services.AddScoped<CourseAssignmentProjectionService>();
        services.AddScoped<LeaveRequestService>();
        services.AddScoped<ResearchProfileService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>faculty</c> schema - called once from the Host composition root, mirroring Organization's own <c>UseOrganizationModuleAsync</c>.</summary>
    public static async Task UseFacultyModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FacultyDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
