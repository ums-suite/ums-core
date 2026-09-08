using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using UMS.Modules.Library.Infrastructure;
using UMS.Modules.Library.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Faculty;
using UMS.Shared.Finance;
using UMS.Shared.Notifications;
using UMS.Shared.Student;

namespace UMS.Modules.Library.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Library's own real composition root (<c>AddLibraryModule</c>/<c>UseLibraryModuleAsync</c>)
/// against a real, disposable Postgres container - Application-service level, not the full HTTP+JWT
/// stack, mirroring Hostel's/Finance's own fixture posture exactly. Every test resolves its own DI
/// scope from <see cref="Services"/> so each gets fresh, scoped repositories/DbContext while sharing
/// the one real Postgres instance.
/// </summary>
public sealed class LibraryServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_library_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeStudentStatusChecker StudentStatusChecker => (FakeStudentStatusChecker)Services.GetRequiredService<IStudentStatusChecker>();

    public FakeFacultyMemberLookup FacultyMemberLookup => (FakeFacultyMemberLookup)Services.GetRequiredService<IFacultyMemberLookup>();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Library:MaxConcurrentLoansStudent"] = "3",
                ["Library:MaxConcurrentLoansFaculty"] = "10",
                ["Library:LoanPeriodDaysStudent"] = "14",
                ["Library:LoanPeriodDaysFaculty"] = "30",
                ["Library:MaxRenewalCount"] = "2",
                ["Library:ReservationClaimWindowHours"] = "48",
                ["Library:FineDailyRateBdt"] = "10",
                ["Library:DefaultReplacementCostBdt"] = "500",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<IAuditRecorder, FakeAuditRecorder>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();
        services.AddSingleton<IInvoiceRequester, FakeInvoiceRequester>();
        services.AddSingleton<IStudentStatusChecker, FakeStudentStatusChecker>();
        services.AddSingleton<IFacultyMemberLookup, FakeFacultyMemberLookup>();

        services.AddLibraryModule(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);
}
