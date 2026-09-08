using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Application.Applications;
using UMS.Modules.Hostel.Application.ApplicationWindows;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Application.Complaints;
using UMS.Modules.Hostel.Application.Hostels;
using UMS.Modules.Hostel.Infrastructure.Adapters;
using UMS.Modules.Hostel.Infrastructure.Authorization;
using UMS.Modules.Hostel.Infrastructure.CrossModule;
using UMS.Modules.Hostel.Infrastructure.Persistence;
using UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;
using UMS.Shared.Authorization;

namespace UMS.Modules.Hostel.Infrastructure;

/// <summary>
/// Composition root for the Hostel module (Flow #19).
///
/// <para>
/// Cross-module dependencies (module-boundaries.md: Hostel depends on Identity, Student, Finance -
/// deliberately NOT Organization, requirement-spec.md §9 decision 1) resolve against their real
/// implementations, already registered by the Host/Workers composition root before
/// <c>AddHostelModule</c> runs - <c>UMS.Shared.Student.IStudentStatusChecker</c>,
/// <c>UMS.Shared.Finance.IInvoiceRequester</c>, and <c>UMS.Shared.Notifications.INotificationRequestIntake</c>
/// are all consumed directly (or, for Notifications, through this module's own
/// <see cref="INotificationRequestPublisher"/> port) - every one of them already has a real
/// implementation by the time this module builds.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddHostelModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<HostelDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "hostel")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<HostelDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<HostelDbContext>());

        services.AddScoped<IHostelRepository, HostelRepository>();
        services.AddScoped<IBuildingRepository, BuildingRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IBedRepository, BedRepository>();
        services.AddScoped<IApplicationWindowRepository, ApplicationWindowRepository>();
        services.AddScoped<IHostelApplicationRepository, HostelApplicationRepository>();
        services.AddScoped<IAllocationRepository, AllocationRepository>();
        services.AddScoped<IAllocationReviewFlagRepository, AllocationReviewFlagRepository>();
        services.AddScoped<IComplaintRepository, ComplaintRepository>();
        services.AddScoped<IOutboxReader, Persistence.Repositories.OutboxReader>();
        services.AddScoped<IFinancePaymentEventSource, FinanceOutboxEventSource>();
        services.AddScoped<IStudentStatusEventSource, StudentOutboxEventSource>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, HostelPermissionManifest>();
        services.AddSingleton(new HostelOptions
        {
            GracePeriodDays = configuration.GetValue<int?>("Hostel:GracePeriodDays") ?? 7,
            ComplaintDedupeWindowSeconds = configuration.GetValue<int?>("Hostel:ComplaintDedupeWindowSeconds") ?? 60,
            ComplaintPostCheckOutGraceDays = configuration.GetValue<int?>("Hostel:ComplaintPostCheckOutGraceDays") ?? 7,
        });

        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();
        services.AddScoped<StudentContextService>();

        services.AddScoped<InventoryService>();
        services.AddScoped<ApplicationWindowService>();
        services.AddScoped<HostelApplicationService>();
        services.AddScoped<HostelApplicationRankingService>();
        services.AddScoped<HostelApplicationReviewService>();
        services.AddScoped<AllocationService>();
        services.AddScoped<AllocationFeeConfirmationService>();
        services.AddScoped<GracePeriodExpiryService>();
        services.AddScoped<WaitlistReRankingService>();
        services.AddScoped<AllocationReviewFlagService>();
        services.AddScoped<ComplaintService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>hostel</c> schema - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseHostelModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostelDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
