using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.FundingBodies;
using UMS.Modules.Research.Application.Grants;
using UMS.Modules.Research.Application.InstitutionalRepositoryEntries;
using UMS.Modules.Research.Application.Publications;
using UMS.Modules.Research.Infrastructure.Adapters;
using UMS.Modules.Research.Infrastructure.Authorization;
using UMS.Modules.Research.Infrastructure.CrossModule;
using UMS.Modules.Research.Infrastructure.Persistence;
using UMS.Modules.Research.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Research.Infrastructure;

/// <summary>
/// Composition root for the Research module (release/DEVELOPMENT_PLAN.md Flow #25).
///
/// <para>
/// module-boundaries.md: Research depends ONLY on Identity, Organization, and Faculty - never
/// Student, never Finance. This composition root itself only ever resolves
/// <c>UMS.Shared.Faculty.IFacultyMemberLookup</c> (every FacultyMemberId reference validated at
/// write time), <c>UMS.Shared.Documents.IUploadedArtifactRequester</c> (RES-11's repository-file
/// upload), and <c>UMS.Shared.Notifications.INotificationRequestIntake</c> (RES-5's PI-vacancy fan
/// out) - all real by the time <c>AddResearchModule</c> runs in the Host/Workers composition root,
/// mirroring every other module's own registration.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddResearchModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ResearchDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "research")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ResearchDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<ResearchDbContext>());

        services.AddScoped<IFundingBodyRepository, FundingBodyRepository>();
        services.AddScoped<IGrantRepository, GrantRepository>();
        services.AddScoped<IPublicationRepository, PublicationRepository>();
        services.AddScoped<IPublicationDuplicateCandidateRepository, PublicationDuplicateCandidateRepository>();
        services.AddScoped<IInstitutionalRepositoryEntryRepository, InstitutionalRepositoryEntryRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();
        services.AddScoped<IFacultyStatusEventSource, FacultyOutboxEventSource>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, ResearchPermissionManifest>();
        services.AddScoped<IResearchNotificationPublisher, NotificationRequestIntakeAdapter>();

        services.AddScoped<FundingBodyService>();
        services.AddScoped<GrantService>();
        services.AddScoped<GrantPiVacancyService>();
        services.AddScoped<PublicationService>();
        services.AddScoped<PublicationDuplicateDetectionService>();
        services.AddScoped<InstitutionalRepositoryEntryService>();
        services.AddScoped<EmbargoLiftService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>research</c> schema and syncs the Permission catalog - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseResearchModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ResearchDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
