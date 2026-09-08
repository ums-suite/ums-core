using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using UMS.Modules.Hostel.Infrastructure;
using UMS.Modules.Hostel.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Finance;
using UMS.Shared.Notifications;
using UMS.Shared.Student;

namespace UMS.Modules.Hostel.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Hostel's own real composition root (<c>AddHostelModule</c>/<c>UseHostelModuleAsync</c>)
/// against a real, disposable Postgres container - Application-service level, not the full HTTP+JWT
/// stack, mirroring Finance's/Admission's own fixture posture exactly. Every test resolves its own
/// DI scope from <see cref="Services"/> so each gets fresh, scoped repositories/DbContext while
/// sharing the one real Postgres instance.
/// </summary>
public sealed class HostelServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_hostel_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeStudentStatusChecker StudentStatusChecker => (FakeStudentStatusChecker)Services.GetRequiredService<IStudentStatusChecker>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Hostel:GracePeriodDays"] = "7",
                ["Hostel:ComplaintDedupeWindowSeconds"] = "60",
                ["Hostel:ComplaintPostCheckOutGraceDays"] = "7",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<IAuditRecorder, FakeAuditRecorder>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();
        services.AddSingleton<IInvoiceRequester, FakeInvoiceRequester>();
        services.AddSingleton<IStudentStatusChecker, FakeStudentStatusChecker>();

        services.AddHostelModule(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HostelDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);
}
