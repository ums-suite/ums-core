using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Alumni;
using UMS.Modules.Alumni.Application.AlumniEvents;
using UMS.Modules.Alumni.Application.Chapters;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Application.Donations;
using UMS.Modules.Alumni.Application.Jobs;
using UMS.Modules.Alumni.Application.Mentorship;
using UMS.Modules.Alumni.Infrastructure.Adapters;
using UMS.Modules.Alumni.Infrastructure.Authorization;
using UMS.Modules.Alumni.Infrastructure.CrossModule;
using UMS.Modules.Alumni.Infrastructure.Persistence;
using UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Alumni.Infrastructure;

/// <summary>
/// Composition root for the Alumni module (release/DEVELOPMENT_PLAN.md Flow #29).
///
/// <para>
/// module-boundaries.md: Alumni depends on Identity, Student (StudentGraduated consumption only,
/// via the shared <c>IStudentStatusChecker</c> read contract - see <c>Alumnus</c>'s own remarks) and
/// Finance (Donation payment delegation, via <c>IInvoiceRequester</c>/the Finance outbox poll). This
/// composition root registers exactly those cross-module dependencies, resolved for real by the
/// time <c>AddAlumniModule</c> runs in the Host/Workers composition root - mirroring every other
/// module's own registration.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAlumniModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<AlumniDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "alumni")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AlumniDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<AlumniDbContext>());

        services.AddScoped<IAlumnusRepository, AlumnusRepository>();
        services.AddScoped<IAlumniChapterRepository, AlumniChapterRepository>();
        services.AddScoped<IJobPostingRepository, JobPostingRepository>();
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<IDonationCampaignRepository, DonationCampaignRepository>();
        services.AddScoped<IDonationRepository, DonationRepository>();
        services.AddScoped<IMentorshipOptInRepository, MentorshipOptInRepository>();
        services.AddScoped<IMentorshipMatchRepository, MentorshipMatchRepository>();
        services.AddScoped<IAlumniEventRepository, AlumniEventRepository>();
        services.AddScoped<IAlumniEventRsvpRepository, AlumniEventRsvpRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddScoped<IStudentGraduatedEventSource, StudentOutboxEventSource>();
        services.AddScoped<IFinancePaymentEventSource, FinanceOutboxEventSource>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, AlumniPermissionManifest>();
        services.AddScoped<IAlumniNotificationPublisher, NotificationRequestIntakeAdapter>();

        services.AddScoped<CallerAlumnusResolver>();
        services.AddScoped<AlumnusService>();
        services.AddScoped<ChapterService>();
        services.AddScoped<JobPostingService>();
        services.AddScoped<JobApplicationService>();
        services.AddScoped<JobPostingExpiryService>();
        services.AddScoped<DonationCampaignService>();
        services.AddScoped<DonationService>();
        services.AddScoped<DonationConfirmationService>();
        services.AddScoped<RecurringDonationSchedulerService>();
        services.AddScoped<MentorshipOptInService>();
        services.AddScoped<MentorshipMatchService>();
        services.AddScoped<AlumniEventService>();
        services.AddScoped<AlumniEventRsvpService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>alumni</c> schema - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseAlumniModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
