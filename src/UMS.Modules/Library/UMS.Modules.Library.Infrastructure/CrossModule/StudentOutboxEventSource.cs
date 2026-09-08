using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Infrastructure.Persistence;
using UMS.Modules.Library.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Library.Infrastructure.CrossModule;

/// <summary>
/// LIB-16: mirrors Hostel's own <c>StudentOutboxEventSource</c> exactly. Student's own
/// <c>StudentDbContext</c> uses EF's DEFAULT PascalCase table/column naming (no explicit
/// <c>OutboxMessageConfiguration</c>), so the physical table is <c>student."OutboxMessages"</c> with
/// quoted PascalCase columns, and <c>EventType</c> is stored as the domain event's FULL CLR type name
/// (<c>domainEvent.GetType().FullName</c>) - matched here by a <c>LIKE</c> suffix, never an exact
/// string or a full assembly-qualified literal (ArchitectureTests' module-boundary convention, even
/// though it only scans compiled type references, not SQL string data - a defensive habit).
/// </summary>
internal sealed class StudentOutboxEventSource(LibraryDbContext context, IConfiguration configuration, ILogger<StudentOutboxEventSource> logger) : IStudentStatusEventSource
{
    private const string Source = "student";
    private const string EventTypeSuffix = "%.StudentStatusChanged";

    public async Task<IReadOnlyList<StudentStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var processedSet = (await context.Set<ProcessedInboundEvent>()
            .Where(e => e.Source == Source)
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value.");

        var envelopes = new List<StudentStatusEventEnvelope>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand(
            """SELECT "Id", "PayloadJson", "OccurredAt" FROM student."OutboxMessages" WHERE "EventType" LIKE @eventTypeSuffix ORDER BY "RecordedAt" LIMIT @limit""",
            connection);
        command.Parameters.AddWithValue("eventTypeSuffix", EventTypeSuffix);
        command.Parameters.AddWithValue("limit", batchSize * 4);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && envelopes.Count < batchSize)
            {
                var eventId = reader.GetGuid(0);
                if (processedSet.Contains(eventId))
                {
                    continue;
                }

                var studentId = ExtractStudentId(reader.GetString(1));
                if (studentId is null)
                {
                    continue;
                }

                envelopes.Add(new StudentStatusEventEnvelope(eventId, studentId.Value, reader.GetFieldValue<DateTimeOffset>(2)));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogDebug("Student outbox table does not exist - reporting zero unprocessed status-change events.");
            return [];
        }

        return envelopes;
    }

    public async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        context.Set<ProcessedInboundEvent>().Add(new ProcessedInboundEvent(eventId, Source, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Guid? ExtractStudentId(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("StudentId", out var studentIdElement) && studentIdElement.TryGetGuid(out var studentId)
            ? studentId
            : null;
    }
}
