using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Application.Exports;
using UMS.Modules.Audit.Application.Retention;
using UMS.Modules.Audit.Infrastructure.Authorization;
using UMS.Modules.Audit.Infrastructure.Partitioning;
using UMS.Modules.Audit.Infrastructure.Persistence;
using UMS.Modules.Audit.Infrastructure.Persistence.Repositories;
using UMS.Modules.Audit.Infrastructure.Retention;
using UMS.Modules.Audit.Infrastructure.Storage;
using UMS.Modules.Audit.Infrastructure.Writing;
using UMS.Shared.Audit;
using UMS.Shared.Authorization;

namespace UMS.Modules.Audit.Infrastructure;

/// <summary>
/// Composition root for the Audit module - mirrors Identity's own <c>DependencyInjection</c>
/// exactly. Also where <see cref="IAuditRecorder"/> (the cross-module write-path contract every
/// other module calls) gets its one real registration, and where <c>UMS.Workers</c> pulls its own
/// export-relay dependencies from (see that project's own <c>Program.cs</c>).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AuditDbContext>());
        services.AddScoped<IOutboxEnqueuer>(sp => sp.GetRequiredService<AuditDbContext>());
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddScoped<IAuditLogEntryRepository, AuditLogEntryRepository>();
        services.AddScoped<IAuditExportRequestRepository, AuditExportRequestRepository>();
        services.AddScoped<IPartitionInspector, PostgresPartitionInspector>();

        services.AddSingleton<IClock, SystemClock>();

        services.Configure<AuditRetentionOptions>(configuration.GetSection("Audit:Retention"));
        services.AddScoped<RetentionPolicyEvaluator>();
        services.AddScoped<AuditRetentionEnforcementService>();
        services.AddScoped<AuditPartitionMaintenanceService>();

        services.Configure<ObjectStorageOptions>(configuration.GetSection("Audit:Storage"));
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        services.AddScoped<AuditQueryService>();
        services.AddScoped<AuditExportService>();

        // AUD-1: the cross-module write-path contract - every other module resolves this same
        // interface from `UMS.Shared.Audit`, never a type from this assembly directly
        // (module-boundaries.md). Stateless (builds its own short-lived DbContext per call, bound
        // to the caller's own transaction - see AuditRecorder's remarks), so Singleton is safe and
        // avoids a scoped-lifetime mismatch with whatever DI scope the calling module resolves it
        // from.
        services.AddSingleton<IAuditRecorder, AuditRecorder>();

        services.AddSingleton<IPermissionManifest, AuditPermissionManifest>();

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations for the <c>audit</c> schema and ensures every monthly
    /// partition the current operating window needs already exists (AUD-3) - called once from the
    /// Host composition root, mirroring Identity's own <c>UseIdentityModuleAsync</c>.
    /// </summary>
    public static async Task UseAuditModuleAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        var partitionMaintenance = scope.ServiceProvider.GetRequiredService<AuditPartitionMaintenanceService>();
        await partitionMaintenance.EnsureFuturePartitionsAsync().ConfigureAwait(false);
    }
}
