using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Application.Banners;
using UMS.Modules.Content.Application.Downloads;
using UMS.Modules.Content.Application.Events;
using UMS.Modules.Content.Application.HomepageSections;
using UMS.Modules.Content.Application.Notices;
using UMS.Modules.Content.Application.Scheduling;
using UMS.Modules.Content.Infrastructure.Adapters;
using UMS.Modules.Content.Infrastructure.Authorization;
using UMS.Modules.Content.Infrastructure.Caching;
using UMS.Modules.Content.Infrastructure.Persistence;
using UMS.Modules.Content.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Content.Infrastructure;

/// <summary>
/// Composition root for the Content module (Flow #24).
///
/// <para>
/// Cross-module dependencies (module-boundaries.md: Content depends on Identity, Organization,
/// Notifications, Documents) resolve against their real implementations, already registered by the
/// Host/Workers composition root before <c>AddContentModule</c> runs -
/// <c>UMS.Shared.Identity.IScopeGrantDirectory</c> (CNT-3 audience scoping),
/// <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c> (CNT-10 homepage reference
/// resolution), <c>UMS.Shared.Documents.IUploadedArtifactRequester</c> (CNT-11), and
/// <c>UMS.Shared.Notifications.INotificationRequestIntake</c> (CNT-13) are all consumed directly or
/// through this module's own adapter, every one of them already real by this point.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddContentModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<ContentDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "content")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ContentDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<ContentDbContext>());

        services.AddScoped<INoticeRepository, NoticeRepository>();
        services.AddScoped<IEventRepository, EventRepository>();
        services.AddScoped<IBannerRepository, BannerRepository>();
        services.AddScoped<IHomepageSectionRepository, HomepageSectionRepository>();
        services.AddScoped<IDownloadResourceRepository, DownloadResourceRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, ContentPermissionManifest>();
        services.AddScoped<ICacheInvalidator, RedisCacheInvalidator>();
        services.AddScoped<INoticeNotificationPublisher, NotificationRequestIntakeAdapter>();

        services.AddScoped<NoticeService>();
        services.AddScoped<EventService>();
        services.AddScoped<BannerService>();
        services.AddScoped<HomepageSectionService>();
        services.AddScoped<DownloadResourceService>();

        services.AddScoped<NoticeSchedulingService>();
        services.AddScoped<BannerSchedulingService>();
        services.AddScoped<DownloadResourceSchedulingService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>content</c> schema - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseContentModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
