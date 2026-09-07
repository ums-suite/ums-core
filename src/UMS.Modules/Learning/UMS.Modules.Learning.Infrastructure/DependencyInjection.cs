using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.Application.Discussions;
using UMS.Modules.Learning.Application.LectureMaterials;
using UMS.Modules.Learning.Application.PlagiarismChecks;
using UMS.Modules.Learning.Application.Submissions;
using UMS.Modules.Learning.Infrastructure.Authorization;
using UMS.Modules.Learning.Infrastructure.Documents;
using UMS.Modules.Learning.Infrastructure.Notifications;
using UMS.Modules.Learning.Infrastructure.Persistence;
using UMS.Modules.Learning.Infrastructure.Persistence.Repositories;
using UMS.Modules.Learning.Infrastructure.Plagiarism;
using UMS.Shared.Authorization;
using UMS.Shared.Resilience.Http;

namespace UMS.Modules.Learning.Infrastructure;

/// <summary>
/// Composition root for the Learning module (release/DEVELOPMENT_PLAN.md Flow #13) - mirrors
/// Academic's own <c>DependencyInjection</c> exactly.
///
/// <para>
/// Cross-module dependencies (module-boundaries.md: Learning depends on Identity, Organization,
/// Academic, Documents, Notifications) are all resolved against their real implementations, already
/// registered by the Host/Workers composition root before <c>AddLearningModule</c> runs - Academic
/// via <c>UMS.Shared.Academic.ICourseOfferingLookup</c> (new in this build), Documents via
/// <c>UMS.Shared.Documents.IUploadedArtifactRequester</c> (also new in this build), Notifications
/// via <c>UMS.Shared.Notifications.INotificationRequestIntake</c>, and Audit via
/// <c>UMS.Shared.Audit.IAuditRecorder</c>. There is no stub anywhere in this module: every contract
/// it needs already has a real implementation, because Learning is the last module in its
/// dependency chain to be built.
/// </para>
///
/// <para>
/// Note what is NOT registered: nothing Academic-facing that writes. design-decisions.md's
/// "Cross-Module Feed of Assignment Scores into Academic's Grade" resolves that
/// <c>SubmissionEvaluated</c> is a fan-out event only - the acyclic 18-module graph depends on that
/// absence.
/// </para>
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<LearningDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres")
                    ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value."),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "learning")));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<LearningDbContext>());
        services.AddScoped<IDomainEventRecorder>(sp => sp.GetRequiredService<LearningDbContext>());

        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<ISubmissionRepository, SubmissionRepository>();
        services.AddScoped<ILectureMaterialRepository, LectureMaterialRepository>();
        services.AddScoped<IDiscussionThreadRepository, DiscussionThreadRepository>();
        services.AddScoped<IOutboxReader, OutboxReader>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPermissionManifest, LearningPermissionManifest>();

        services.AddScoped<IUploadedArtifactGateway, UploadedArtifactGatewayAdapter>();
        services.AddScoped<INotificationRequestPublisher, NotificationRequestIntakeAdapter>();

        RegisterFakePlagiarismGateway(services, configuration);

        services.AddScoped<AssignmentService>();
        services.AddScoped<AssignmentQueryService>();
        services.AddScoped<AssignmentWindowCloseService>();
        services.AddScoped<SubmissionService>();
        services.AddScoped<SubmissionQueryService>();
        services.AddScoped<SubmissionUploadService>();
        services.AddScoped<PlagiarismCheckService>();
        services.AddScoped<LectureMaterialService>();
        services.AddScoped<LectureMaterialQueryService>();
        services.AddScoped<DiscussionService>();

        return services;
    }

    /// <summary>Applies pending EF Core migrations for the <c>learning</c> schema - called once from the Host/Workers composition root, mirroring Academic's own <c>UseAcademicModuleAsync</c>.</summary>
    public static async Task UseLearningModuleAsync(this IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LearningDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// LRN-8: registers the similarity-check provider's typed <c>HttpClient</c> with
    /// <c>UMS.Shared.Resilience</c>'s standard Polly pipeline (retry with exponential backoff +
    /// jitter, circuit breaker, timeout) wrapped around <see cref="FakePlagiarismProviderHandler"/>
    /// as the innermost (primary) handler - see that class's own remarks for why this build fakes
    /// the provider rather than integrating a real vendor account, and Notifications'
    /// <c>RegisterFakeChannelGateways</c> for the pattern this mirrors.
    /// </summary>
    private static void RegisterFakePlagiarismGateway(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FakePlagiarismGatewayOptions>(configuration.GetSection("Learning:PlagiarismProvider"));

        services.AddUmsResilientHttpClient<HttpPlagiarismCheckProvider>()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("https://fake-similarity-gateway.ums-suite.internal/"))
            .ConfigurePrimaryHttpMessageHandler(sp => new FakePlagiarismProviderHandler(sp.GetRequiredService<IOptionsMonitor<FakePlagiarismGatewayOptions>>()));

        services.AddScoped<IPlagiarismCheckProvider>(sp => sp.GetRequiredService<HttpPlagiarismCheckProvider>());
    }
}
