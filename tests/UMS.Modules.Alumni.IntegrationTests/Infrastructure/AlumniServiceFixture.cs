using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using UMS.Modules.Alumni.Infrastructure;
using UMS.Modules.Alumni.Infrastructure.Persistence;
using UMS.Shared.Audit;
using UMS.Shared.Finance;
using UMS.Shared.Notifications;
using UMS.Shared.Student;

namespace UMS.Modules.Alumni.IntegrationTests.Infrastructure;

/// <summary>
/// Boots Alumni's own real composition root (<c>AddAlumniModule</c>/<c>UseAlumniModuleAsync</c>)
/// against a real, disposable Postgres container - Application-service level, not the full HTTP+JWT
/// stack, mirroring Research's/Content's/Hostel's/Library's own fixture posture exactly.
///
/// <para>
/// Also creates bare-bones <c>student."OutboxMessages"</c> (EF-default PascalCase, mirroring
/// Student's own real shape) and <c>finance.outbox_messages</c> (snake_case, mirroring Finance's own
/// real shape) tables by hand - Student's/Finance's own modules are never installed in this fixture -
/// so <see cref="Alumni.Infrastructure.CrossModule.StudentOutboxEventSource"/>'s and
/// <see cref="Alumni.Infrastructure.CrossModule.FinanceOutboxEventSource"/>'s real raw-SQL queries
/// have real tables to poll for the ALM-1/ALM-9 consumed-event tests.
/// </para>
/// </summary>
public sealed class AlumniServiceFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("ums_alumni_it")
        .WithUsername("ums")
        .WithPassword("ums_test_password")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public FakeStudentStatusChecker StudentStatusChecker => (FakeStudentStatusChecker)Services.GetRequiredService<IStudentStatusChecker>();

    public FakeInvoiceRequester InvoiceRequester => (FakeInvoiceRequester)Services.GetRequiredService<IInvoiceRequester>();

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
        services.AddSingleton<IInvoiceRequester, FakeInvoiceRequester>();
        services.AddSingleton<INotificationRequestIntake, FakeNotificationRequestIntake>();

        services.AddAlumniModule(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlumniDbContext>();
        await dbContext.Database.MigrateAsync().ConfigureAwait(false);

        await CreateStudentOutboxTableAsync().ConfigureAwait(false);
        await CreateFinanceOutboxTableAsync().ConfigureAwait(false);
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

    /// <summary>Mirrors Finance's own snake_case <c>outbox_messages</c> shape exactly.</summary>
    public async Task InsertFinanceOutboxMessageAsync(Guid id, string eventType, string payloadJson, DateTimeOffset occurredAt, DateTimeOffset recordedAt)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using var command = new NpgsqlCommand(
            "INSERT INTO finance.outbox_messages (id, event_type, payload_json, occurred_at, recorded_at) VALUES (@id, @eventType, @payloadJson::jsonb, @occurredAt, @recordedAt)",
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

    private async Task CreateFinanceOutboxTableAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        await using (var schemaCommand = new NpgsqlCommand("CREATE SCHEMA IF NOT EXISTS finance;", connection))
        {
            await schemaCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using var tableCommand = new NpgsqlCommand(
            """
            CREATE TABLE IF NOT EXISTS finance.outbox_messages (
                id uuid PRIMARY KEY,
                event_type text NOT NULL,
                payload_json jsonb NOT NULL,
                occurred_at timestamptz NOT NULL,
                recorded_at timestamptz NOT NULL
            );
            """,
            connection);
        await tableCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
