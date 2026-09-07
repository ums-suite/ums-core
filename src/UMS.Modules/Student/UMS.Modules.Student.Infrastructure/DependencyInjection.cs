using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.Guardians;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.Infrastructure.Authorization;
using UMS.Modules.Student.Infrastructure.CrossModule;
using UMS.Modules.Student.Infrastructure.Persistence;
using UMS.Modules.Student.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;
using UMS.Shared.Student;

namespace UMS.Modules.Student.Infrastructure;

/// <summary>
/// Composition root for the Student module (release/DEVELOPMENT_PLAN.md Flow #11) - mirrors
/// Faculty's own <c>DependencyInjection</c> exactly. No other module or the Host itself ever
/// references <c>UMS.Modules.Student.Domain</c>/<c>.Infrastructure</c> directly
/// (module-boundaries.md) - other modules go through <c>UMS.Shared.Student</c> instead, the same
/// way every module reaches Audit through <c>UMS.Shared.Audit</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddStudentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<StudentDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "student")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<StudentDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<StudentDbContext>());

        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();
        services.AddScoped<IStudentNumberSequence, StudentNumberSequence>();

        services.AddSingleton<IClock, SystemClock>();

        // The real cross-module implementations - Organization (Flow #6), Identity (Flow #4),
        // Documents (Flow #9), and Notifications (Flow #8) all already exist, so every one of
        // these is a real integration, not a stub (see this module's own PR "Known gaps" section
        // for the one deliberate exception, IProgramExistenceChecker, below).
        services.AddScoped<IOrganizationDepartmentExistenceChecker, OrganizationDepartmentExistenceCheckerAdapter>();
        services.AddScoped<IUserProvisioningPort, UserProvisioningPortAdapter>();
        services.AddScoped<IDocumentGenerationPort, DocumentGenerationPortAdapter>();
        services.AddScoped<Application.Abstractions.INotificationRequestPublisher, NotificationRequestIntakeAdapter>();

        // Academic (Flow #12) does not exist yet - see StubProgramExistenceChecker's own remarks.
        services.AddScoped<IProgramExistenceChecker, StubProgramExistenceChecker>();

        // STU-1: the outward-facing shared contract Admission (Flow #15) will call once it exists.
        services.AddScoped<IStudentRecordProvisioner, StudentRecordProvisionerAdapter>();

        // ACD-6 (release/DEVELOPMENT_PLAN.md Flow #12, Academic): the outward-facing query
        // contract Academic's Enrollment gate calls to validate Student status/scope - the first
        // real caller of this contract, registered directly (no stub-then-promote dance needed).
        services.AddScoped<IStudentStatusChecker, StudentStatusCheckerAdapter>();

        services.AddSingleton<IPermissionManifest, StudentPermissionManifest>();

        services.AddScoped<CreateStudentRecordService>();
        services.AddScoped<StudentProfileService>();
        services.AddScoped<StudentStatusService>();
        services.AddScoped<GuardianService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>student</c> schema - called once from the Host composition root, mirroring Faculty's own <c>UseFacultyModuleAsync</c>.</summary>
    public static async Task UseStudentModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StudentDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
