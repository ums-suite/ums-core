using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using UMS.Modules.Content.Infrastructure;
using UMS.Modules.Content.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.Identity;
using UMS.Shared.Notifications;
using UMS.Shared.Organization;

namespace UMS.Modules.Content.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Content's own real composition root (<c>AddContentModule</c>/<c>UseContentModuleAsync</c>)
/// against a real, disposable Postgres container - Application-service level, not the full HTTP+JWT
/// stack, mirroring Hostel's/Finance's own fixture posture exactly. Every test resolves its own DI
/// scope from <see cref="Services"/> so each gets fresh, scoped repositories/DbContext while sharing
/// the one real Postgres instance.
/// </summary>
public sealed class ContentServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_content_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeScopeGrantDirectory ScopeGrants => (FakeScopeGrantDirectory)Services.GetRequiredService<IScopeGrantDirectory>();

    public FakeOrganizationNodeExistenceChecker OrganizationNodes => (FakeOrganizationNodeExistenceChecker)Services.GetRequiredService<IOrganizationNodeExistenceChecker>();

    public FakeUploadedArtifactRequester UploadedArtifacts => (FakeUploadedArtifactRequester)Services.GetRequiredService<IUploadedArtifactRequester>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<IAuditRecorder, FakeAuditRecorder>();
        services.AddSingleton<IScopeGrantDirectory, FakeScopeGrantDirectory>();
        services.AddSingleton<IOrganizationNodeExistenceChecker, FakeOrganizationNodeExistenceChecker>();
        services.AddSingleton<IUploadedArtifactRequester, FakeUploadedArtifactRequester>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();

        services.AddContentModule(configuration);

        // Overrides AddContentModule's own RedisCacheInvalidator registration - the last
        // registration for a service type wins on resolution, and this fixture deliberately
        // doesn't stand up a real IConnectionMultiplexer (see FakeCacheInvalidator's own remarks).
        services.AddScoped<UMS.Modules.Content.Application.Abstractions.ICacheInvalidator, FakeCacheInvalidator>();

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ContentDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);
}
