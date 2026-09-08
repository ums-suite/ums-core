using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using UMS.Modules.Reporting.Infrastructure;
using UMS.Modules.Reporting.Infrastructure.Persistence;
using UMS.Shared.Academic;
using UMS.Shared.Admission;
using UMS.Shared.Audit;
using UMS.Shared.Content;
using UMS.Shared.Documents;
using UMS.Shared.Faculty;
using UMS.Shared.Finance;
using UMS.Shared.Hostel;
using UMS.Shared.Library;
using UMS.Shared.Notifications;
using UMS.Shared.Research;

namespace UMS.Modules.Reporting.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Reporting's own real composition root (<c>AddReportingModule</c>/<c>UseReportingModuleAsync</c>)
/// against real, disposable Postgres AND Redis containers - Application-service level, not the full
/// HTTP+JWT stack, mirroring Admission's own <c>AdmissionServiceFixture</c> exactly. A real Redis is
/// needed (not a fake) because <c>UMS.Modules.Reporting.Infrastructure.Caching.RedisMetricRefreshLease</c>'s
/// own distributed lock is exactly what this suite's own concurrent-tick race test verifies.
/// </summary>
public sealed class ReportingServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_reporting_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeAcademicReportingQuery AcademicQuery { get; } = new();

    public FakeAdmissionReportingQuery AdmissionQuery { get; } = new();

    public FakeFinanceReportingQuery FinanceQuery { get; } = new();

    public FakeFacultyReportingQuery FacultyQuery { get; } = new();

    public FakeHostelReportingQuery HostelQuery { get; } = new();

    public FakeLibraryReportingQuery LibraryQuery { get; } = new();

    public FakeResearchReportingQuery ResearchQuery { get; } = new();

    public FakeContentReportingQuery ContentQuery { get; } = new();

    public FakeAuditRecorder AuditRecorder { get; } = new();

    public FakeDocumentGenerationRequester DocumentGenerationRequester { get; } = new();

    public FakeNotificationRequestIntake NotificationIntake { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["ConnectionStrings:Redis"] = _redis.GetConnectionString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(_redis.GetConnectionString()));

        services.AddSingleton<IAcademicReportingQuery>(AcademicQuery);
        services.AddSingleton<IAdmissionReportingQuery>(AdmissionQuery);
        services.AddSingleton<IFinanceReportingQuery>(FinanceQuery);
        services.AddSingleton<IFacultyReportingQuery>(FacultyQuery);
        services.AddSingleton<IHostelReportingQuery>(HostelQuery);
        services.AddSingleton<ILibraryReportingQuery>(LibraryQuery);
        services.AddSingleton<IResearchReportingQuery>(ResearchQuery);
        services.AddSingleton<IContentReportingQuery>(ContentQuery);
        services.AddSingleton<IAuditRecorder>(AuditRecorder);
        services.AddSingleton<IDocumentGenerationRequester>(DocumentGenerationRequester);
        services.AddSingleton<INotificationRequestIntake>(NotificationIntake);

        services.AddReportingModule(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
    }
}
