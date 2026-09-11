using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using UMS.Modules.Career.Infrastructure;
using UMS.Modules.Career.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Documents;
using UMS.Shared.Notifications;
using UMS.Shared.Organization;
using UMS.Shared.Student;

namespace UMS.Modules.Career.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Career's own real composition root (<c>AddCareerModule</c>/<c>UseCareerModuleAsync</c>)
/// against a real, disposable Postgres container - Application-service level, not the full HTTP+JWT
/// stack, mirroring Alumni's/Research's/Hostel's own fixture posture exactly.
///
/// <para>
/// Also creates a bare-bones <c>student."OutboxMessages"</c> table (EF-default PascalCase, mirroring
/// Student's own real shape) by hand - Student's own module is never installed in this fixture - so
/// <see cref="Career.Infrastructure.CrossModule.StudentOutboxEventSource"/>'s real raw-SQL queries
/// have a real table to poll for CAR-16's consumed-event tests.
/// </para>
/// </summary>
public sealed class CareerServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_career_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeStudentStatusChecker StudentStatusChecker => (FakeStudentStatusChecker)Services.GetRequiredService<IStudentStatusChecker>();

    public FakeRoomExistenceChecker RoomExistenceChecker => (FakeRoomExistenceChecker)Services.GetRequiredService<IRoomExistenceChecker>();

    public FakeNotificationRequestIntake NotificationRequestIntake => (FakeNotificationRequestIntake)Services.GetRequiredService<INotificationRequestIntake>();

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
        services.AddSingleton<IStudentStatusChecker, FakeStudentStatusChecker>();
        services.AddSingleton<IRoomExistenceChecker, FakeRoomExistenceChecker>();
        services.AddSingleton<IUploadedArtifactRequester, FakeUploadedArtifactRequester>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();

        services.AddCareerModule(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CareerDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        await CreateStudentOutboxTableAsync().ConfigureAwait(false);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync().ConfigureAwait(false);

    /// <summary>Mirrors Student's own EF-default PascalCase <c>OutboxMessages</c> shape exactly.</summary>
    public async Task InsertStudentOutboxMessageAsync(Guid id, string eventType, string payloadJson, DateTimeOffset occurredAt, DateTimeOffset recordedAt)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = new NpgsqlCommand(
            """INSERT INTO student."OutboxMessages" ("Id", "EventType", "PayloadJson", "OccurredAt", "RecordedAt") VALUES (@id, @eventType, @payloadJson::jsonb, @occurredAt, @recordedAt)""",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("payloadJson", payloadJson);
        command.Parameters.AddWithValue("occurredAt", occurredAt);
        command.Parameters.AddWithValue("recordedAt", recordedAt);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task CreateStudentOutboxTableAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var schemaCommand = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS student;", connection))
        {
            await schemaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using var tableCommand = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS student."OutboxMessages" (
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
