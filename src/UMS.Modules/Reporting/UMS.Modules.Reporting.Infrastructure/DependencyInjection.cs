using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.DashboardMetrics;
using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Modules.Reporting.Infrastructure.Authorization;
using UMS.Modules.Reporting.Infrastructure.Caching;
using UMS.Modules.Reporting.Infrastructure.CrossModule;
using UMS.Modules.Reporting.Infrastructure.Persistence;
using UMS.Modules.Reporting.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Reporting.Infrastructure;

/// <summary>
/// Composition root for the Reporting module (release/DEVELOPMENT_PLAN.md Flow #22, topped up by
/// Flow #26 "Reporting - Content &amp; Research top-up").
///
/// <para>
/// Reporting is the one module permitted a "depends on everything" shape (ADR-0013) - but every one
/// of its now eleven (of the eleven named in requirement-spec.md §7; Alumni remains a future flow,
/// out of scope here) source-module dependencies is a READ-ONLY cross-module query contract
/// (<c>UMS.Shared.Academic.IAcademicReportingQuery</c> et al.), each already registered by the time
/// this module builds (the Host/Workers composition root registers every source module before
/// Reporting - see each one's own <c>AddXModule</c> remarks). This composition root therefore
/// resolves them directly, with NO stub anywhere - every dependency is real by construction,
/// mirroring Library's/Hostel's own "everything it depends on already exists" posture. Flow #26
/// added <c>UMS.Shared.Research.IResearchReportingQuery</c> (replacing the base flow's
/// Faculty-headcount proxy for the "Research" regulatory category) and
/// <c>UMS.Shared.Content.IContentReportingQuery</c> (a new, seventh admin dashboard - a documented
/// scope extension, see that contract's own remarks).
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ReportingDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "reporting")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ReportingDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<ReportingDbContext>());

        services.AddScoped<IDashboardMetricRepository, DashboardMetricRepository>();
        services.AddScoped<IRegulatoryReportDefinitionRepository, RegulatoryReportDefinitionRepository>();
        services.AddScoped<IRegulatoryReportRunRepository, RegulatoryReportRunRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, ReportingPermissionManifest>();
        services.AddSingleton<IMetricRefreshLease, RedisMetricRefreshLease>();

        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();

        services.AddScoped<DashboardMetricReadService>();
        services.AddScoped<AcademicDashboardRefreshService>();
        services.AddScoped<AdmissionDashboardRefreshService>();
        services.AddScoped<FinancialDashboardRefreshService>();
        services.AddScoped<FacultyDashboardRefreshService>();
        services.AddScoped<HostelDashboardRefreshService>();
        services.AddScoped<LibraryDashboardRefreshService>();
        services.AddScoped<ResearchDashboardRefreshService>();
        services.AddScoped<ContentDashboardRefreshService>();

        services.AddScoped<RegulatoryReportDefinitionService>();
        services.AddScoped<RegulatoryReportRunService>();
        services.AddScoped<RegulatoryReportRunExecutionService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>reporting</c> schema and idempotently seeds RPT-14's starting RegulatoryReportDefinition catalog - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseReportingModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        await SeedRegulatoryReportDefinitionCatalogAsync(scope.ServiceProvider).ConfigureAwait(false);
    }

    /// <summary>RPT-14: unaudited system bootstrap data - mirrors how other modules seed reference/catalog data without a per-row Audit entry (e.g. Identity's own permission-catalog sync).</summary>
    private static async Task SeedRegulatoryReportDefinitionCatalogAsync(IServiceProvider services)
    {
        var repository = services.GetRequiredService<IRegulatoryReportDefinitionRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var clock = services.GetRequiredService<IClock>();

        var systemUserId = Guid.Empty;
        var now = clock.UtcNow;
        var anyAdded = false;

        foreach (var seed in RegulatoryReportDefinitionCatalogSeeder.Catalog)
        {
            if (await repository.ExistsByNameAsync(seed.Name, default).ConfigureAwait(false))
            {
                continue;
            }

            var created = RegulatoryReportDefinition.Create(
                seed.Name,
                seed.Category,
                seed.Fields,
                filtersJson: "{}",
                sourceQueryReferencesJson: seed.SourceQueryReferencesJson,
                supportedFormats: RegulatoryReportFormat.Pdf | RegulatoryReportFormat.Csv,
                createdByUserId: systemUserId,
                now: now);

            if (created.IsSuccess)
            {
                repository.Add(created.Value);
                anyAdded = true;
            }
        }

        if (anyAdded)
        {
            await unitOfWork.SaveChangesAsync(default).ConfigureAwait(false);
        }
    }
}
