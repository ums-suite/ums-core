using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Applications;
using UMS.Modules.Career.Application.Drives;
using UMS.Modules.Career.Application.Employers;
using UMS.Modules.Career.Application.Internships;
using UMS.Modules.Career.Application.ResumeProfiles;
using UMS.Modules.Career.Application.StudentGraduation;
using UMS.Modules.Career.Infrastructure.Adapters;
using UMS.Modules.Career.Infrastructure.Authorization;
using UMS.Modules.Career.Infrastructure.CrossModule;
using UMS.Modules.Career.Infrastructure.Persistence;
using UMS.Modules.Career.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Career.Infrastructure;

/// <summary>
/// Composition root for the Career module (release/DEVELOPMENT_PLAN.md Flow #30).
///
/// <para>
/// module-boundaries.md: Career depends on Identity, Student (`Student.status` live reads via
/// `IStudentStatusChecker`, plus `StudentGraduated`/`StudentStatusChanged` outbox consumption - never
/// a live schema query), Organization (`IRoomExistenceChecker` for a Drive's venue), and Documents
/// (`IUploadedArtifactRequester` for `ResumeProfile` file storage) - ZERO dependency on Alumni,
/// Academic, or Finance. This composition root registers exactly those cross-module dependencies,
/// resolved for real by the time <c>AddCareerModule</c> runs in the Host/Workers composition root -
/// mirroring every other module's own registration. `IRoomExistenceChecker`/`IUploadedArtifactRequester`
/// themselves are registered by Organization's/Documents' own Infrastructure layers, not here.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddCareerModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<CareerDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "career")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<CareerDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<CareerDbContext>());

        services.AddScoped<IEmployerProfileRepository, EmployerProfileRepository>();
        services.AddScoped<IInternshipRepository, InternshipRepository>();
        services.AddScoped<ICampusRecruitmentDriveRepository, CampusRecruitmentDriveRepository>();
        services.AddScoped<IInterviewSlotRepository, InterviewSlotRepository>();
        services.AddScoped<ICareerApplicationRepository, CareerApplicationRepository>();
        services.AddScoped<IResumeProfileRepository, ResumeProfileRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddScoped<IStudentStatusEventSource, StudentOutboxEventSource>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, CareerPermissionManifest>();
        services.AddScoped<ICareerNotificationPublisher, NotificationRequestIntakeAdapter>();

        services.AddScoped<EmployerProfileService>();
        services.AddScoped<InternshipService>();
        services.AddScoped<InternshipDeadlineSweepService>();
        services.AddScoped<CampusRecruitmentDriveService>();
        services.AddScoped<InterviewSlotService>();
        services.AddScoped<InternshipApplicationService>();
        services.AddScoped<DriveApplicationService>();
        services.AddScoped<CareerApplicationReviewService>();
        services.AddScoped<InternshipWithdrawalCascadeHandler>();
        services.AddScoped<DriveCancellationCascadeHandler>();
        services.AddScoped<ResumeProfileService>();
        services.AddScoped<StudentStatusEventConsumerService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>career</c> schema - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseCareerModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CareerDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
