using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Application.Applicants;
using UMS.Modules.Admission.Application.Applications;
using UMS.Modules.Admission.Application.Campaigns;
using UMS.Modules.Admission.Application.ExamAttempts;
using UMS.Modules.Admission.Application.MeritLists;
using UMS.Modules.Admission.Application.Results;
using UMS.Modules.Admission.Application.Tests;
using UMS.Modules.Admission.Infrastructure.Adapters;
using UMS.Modules.Admission.Infrastructure.Authorization;
using UMS.Modules.Admission.Infrastructure.Caching;
using UMS.Modules.Admission.Infrastructure.CrossModule;
using UMS.Modules.Admission.Infrastructure.Persistence;
using UMS.Modules.Admission.Infrastructure.Persistence.Repositories;
using UMS.Modules.Admission.Infrastructure.Proctoring;
using UMS.Shared.Authorization;
using UMS.Shared.Integrations;

namespace UMS.Modules.Admission.Infrastructure;

/// <summary>
/// Composition root for the Admission module (release/DEVELOPMENT_PLAN.md Flow #15).
///
/// <para>
/// Cross-module dependencies (module-boundaries.md: Admission depends on Identity, Organization,
/// Finance, Documents, Notifications) resolve against their real implementations, already
/// registered by the Host/Workers composition root before <c>AddAdmissionModule</c> runs -
/// <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c>,
/// <c>UMS.Shared.Finance.IInvoiceRequester</c>, <c>UMS.Shared.Documents.IDocumentGenerationRequester</c>,
/// <c>UMS.Shared.Identity.IUserProvisioner</c>, and
/// <c>UMS.Shared.Notifications.INotificationRequestIntake</c> are all consumed directly (or, for
/// Notifications, through this module's own <see cref="INotificationRequestPublisher"/> port) with
/// no stub anywhere - every one of them already has a real implementation by the time this module
/// builds. <see cref="UMS.Shared.Student.IStudentRecordProvisioner"/> is Student's own real,
/// already-shipped contract - this build is its first real in-process caller (ADM-21).
/// </para>
///
/// <para>
/// <see cref="IProctoringProvider"/> (ADR-0018) is the one exception: Admission is the FIRST
/// consumer of this shared contract, and per that ADR's own text ("provider selection ... is a
/// defensible engineering default to make at Admission's own build time"), THIS module's own
/// composition root registers the fake implementation - not the shared library itself, which only
/// defines the interface.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddAdmissionModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<AdmissionDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "admission")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AdmissionDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<AdmissionDbContext>());

        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IApplicantRepository, ApplicantRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IAdmissionTestRepository, AdmissionTestRepository>();
        services.AddScoped<IExamAttemptRepository, ExamAttemptRepository>();
        services.AddScoped<IMeritListRepository, MeritListRepository>();
        services.AddScoped<IAdmissionResultRepository, AdmissionResultRepository>();
        services.AddScoped<IPublishJobRepository, PublishJobRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();
        services.AddScoped<IFinancePaymentEventSource, FinanceOutboxEventSource>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, AdmissionPermissionManifest>();
        services.AddSingleton<IProctoringProvider, FakeProctoringProvider>();

        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();
        services.AddScoped<IResultCache, RedisResultCache>();

        services.AddScoped<CampaignService>();
        services.AddScoped<ApplicantService>();
        services.AddScoped<AdmitCardService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<ApplicationPaymentConfirmationService>();
        services.AddScoped<AdmissionTestService>();
        services.AddScoped<ExamAttemptService>();
        services.AddScoped<ExamAttemptTimeoutSweepService>();
        services.AddScoped<MeritListService>();
        services.AddScoped<AdmissionResultService>();
        services.AddScoped<PublishJobService>();
        services.AddScoped<ResultSearchService>();
        services.AddScoped<WaitlistPromotionService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>admission</c> schema - called once from the Host/Workers composition root, mirroring every other module's own <c>UseXModuleAsync</c>.</summary>
    public static async Task UseAdmissionModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }
}
