using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using UMS.Modules.Finance.Infrastructure;
using UMS.Modules.Finance.Infrastructure.Gateway;
using UMS.Modules.Finance.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.Notifications;

namespace UMS.Modules.Finance.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Finance's own real composition root (<c>AddFinanceModule</c>/<c>UseFinanceModuleAsync</c>)
/// against a real, disposable Postgres container - Application-service level, not the full HTTP+JWT
/// stack (see <c>FakeCrossModuleAdapters</c>' own remarks for why). Every test in this suite resolves
/// its own DI scope from <see cref="Services"/> so each gets fresh, scoped repositories/DbContext
/// while sharing the one real Postgres instance and one real fake-gateway state store.
/// </summary>
public sealed class FinanceServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_finance_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeSslCommerzGatewayState GatewayState => Services.GetRequiredService<FakeSslCommerzGatewayState>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Finance:PaymentGateway:MinLatencyMs"] = "0",
                ["Finance:PaymentGateway:MaxLatencyMs"] = "1",
                ["Finance:PaymentGateway:WebhookSigningSecret"] = "integration-test-webhook-secret",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<IAuditRecorder, FakeAuditRecorder>();
        services.AddSingleton<IDocumentGenerationRequester, FakeDocumentGenerationRequester>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();

        services.AddFinanceModule(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync().ConfigureAwait(false);
    }
}
