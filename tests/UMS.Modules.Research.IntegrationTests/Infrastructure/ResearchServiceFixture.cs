using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using UMS.Modules.Research.Infrastructure;
using UMS.Modules.Research.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.Faculty;
using UMS.Shared.Notifications;

namespace UMS.Modules.Research.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Research's own real composition root (<c>AddResearchModule</c>/<c>UseResearchModuleAsync</c>)
/// against a real, disposable Postgres container - Application-service level, not the full HTTP+JWT
/// stack, mirroring Content's/Hostel's/Library's own fixture posture exactly. Every test resolves its
/// own DI scope from <see cref="Services"/> so each gets fresh, scoped repositories/DbContext while
/// sharing the one real Postgres instance.
///
/// <para>
/// Also creates a bare-bones <c>faculty."OutboxMessages"</c> table by hand (Faculty's OWN module is
/// never installed in this fixture - see <c>FakeCrossModuleAdapters</c>'s own remarks) so
/// <see cref="Research.Infrastructure.CrossModule.FacultyOutboxEventSource"/>'s real raw-SQL query
/// has a real table to poll for the RES-5 consumed-event test.
/// </para>
/// </summary>
public sealed class ResearchServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_research_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeFacultyMemberLookup FacultyMembers => (FakeFacultyMemberLookup)Services.GetRequiredService<IFacultyMemberLookup>();

    public FakeUploadedArtifactRequester UploadedArtifacts => (FakeUploadedArtifactRequester)Services.GetRequiredService<IUploadedArtifactRequester>();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync().ConfigureAwait(false);
        ConnectionString = _postgres.GetConnectionString();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
        services.AddSingleton<IConfiguration>(configuration);

        services.AddSingleton<IAuditRecorder, FakeAuditRecorder>();
        services.AddSingleton<IFacultyMemberLookup, FakeFacultyMemberLookup>();
        services.AddSingleton<IUploadedArtifactRequester, FakeUploadedArtifactRequester>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();

        services.AddResearchModule(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ResearchDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        await CreateFacultyOutboxTableAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    /// <summary>Mirrors Faculty's own EF-default PascalCase <c>OutboxMessages</c> shape exactly (see <c>FacultyOutboxEventSource</c>'s own remarks) - just enough of the real table for that raw-SQL query to run against.</summary>
    public async Task InsertFacultyOutboxMessageAsync(Guid id, string eventType, string payloadJson, DateTimeOffset occurredAt, DateTimeOffset recordedAt)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = new NpgsqlCommand(
            """INSERT INTO faculty."OutboxMessages" ("Id", "EventType", "PayloadJson", "OccurredAt", "RecordedAt") VALUES (@id, @eventType, @payloadJson::jsonb, @occurredAt, @recordedAt)""",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("payloadJson", payloadJson);
        command.Parameters.AddWithValue("occurredAt", occurredAt);
        command.Parameters.AddWithValue("recordedAt", recordedAt);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task CreateFacultyOutboxTableAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var schemaCommand = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS faculty;", connection))
        {
            await schemaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using var tableCommand = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS faculty."OutboxMessages" (
                "Id" uuid PRIMARY KEY,
                "EventType" text NOT NULL,
                "PayloadJson" jsonb NOT NULL,
                "OccurredAt" timestamptz NOT NULL,
                "RecordedAt" timestamptz NOT NULL
            );
            """,
            connection);
        await tableCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
