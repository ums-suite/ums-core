using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using UMS.Modules.Admission.Infrastructure;
using UMS.Modules.Admission.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.Finance;
using UMS.Shared.Identity;
using UMS.Shared.Notifications;
using UMS.Shared.Organization;
using UMS.Shared.Student;

namespace UMS.Modules.Admission.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Admission's own real composition root (<c>AddAdmissionModule</c>/<c>UseAdmissionModuleAsync</c>)
/// against real, disposable Postgres AND Redis containers - Application-service level, not the full
/// HTTP+JWT stack, mirroring Finance's own <c>FinanceServiceFixture</c> exactly. A real Redis is
/// needed here (unlike Finance's own fixture) because <c>RedisResultCache</c>'s atomic Lua write
/// and distributed lock are exactly what this suite's own Results tests verify.
/// </summary>
public sealed class AdmissionServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_admission_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    public IServiceProvider Services { get; private set; } = null!;

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

        services.AddSingleton<IAuditRecorder, FakeAuditRecorder>();
        services.AddSingleton<IOrganizationNodeExistenceChecker, FakeOrganizationNodeExistenceChecker>();
        services.AddSingleton<IInvoiceRequester, FakeInvoiceRequester>();
        services.AddSingleton<IDocumentGenerationRequester, FakeDocumentGenerationRequester>();
        services.AddSingleton<IUserProvisioner, FakeUserProvisioner>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();
        services.AddSingleton<IStudentRecordProvisioner, FakeStudentRecordProvisioner>();

        services.AddAdmissionModule(configuration);

        // AddAdmissionModule registers its own real FakeProctoringProvider (ADR-0018's own posture -
        // Admission owns this provider-selection default, see DependencyInjection's own remarks), so
        // this fixture does not override IProctoringProvider.

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
        await _redis.DisposeAsync().ConfigureAwait(false);
    }
}
