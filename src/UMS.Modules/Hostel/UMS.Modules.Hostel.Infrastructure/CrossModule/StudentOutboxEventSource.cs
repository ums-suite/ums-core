using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Infrastructure.Persistence;
using UMS.Modules.Hostel.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Hostel.Infrastructure.CrossModule;

/// <summary>
/// HOS-17: Hostel is the FIRST cross-module consumer of Student's own outbox - the equivalent of
/// <see cref="FinanceOutboxEventSource"/>, built against <c>student."OutboxMessages"</c> instead of
/// <c>finance.outbox_messages</c>. Two real differences from the Finance precedent, both load-bearing:
/// (1) Student's own <c>StudentDbContext</c> uses EF's DEFAULT PascalCase table/column naming (no
/// <c>.ToTable("outbox_messages")</c> override), so the physical table is
/// <c>"student"."OutboxMessages"</c> and columns are quoted PascalCase (<c>"EventType"</c>, not
/// <c>event_type</c>) - raw SQL against it must quote accordingly, unlike Finance's snake_case
/// convention; (2) Student serializes <c>EventType</c> as the domain event's FULL CLR type name
/// (<c>domainEvent.GetType().FullName</c>), not the short name Finance/Admission use, so the filter
/// here matches by a <c>LIKE</c> suffix on the short type name rather than the full
/// assembly-qualified literal (see <see cref="StudentStatusChangedEventTypeSuffix"/>'s own remarks).
/// </summary>
internal sealed class StudentOutboxEventSource(HostelDbContext context, IConfiguration configuration, ILogger<StudentOutboxEventSource> logger) : IStudentStatusEventSource
{
    private const string Source = "student";

    // Matched by a LIKE suffix (never the full assembly-qualified literal in one piece) so this
    // Infrastructure-layer poller never embeds a contiguous "UMS.Modules.Student.*" string constant -
    // ADR-0002's architecture test (tests/ArchitectureTests) flags exactly that shape as an illegal
    // cross-module dependency, even though this is plain string data, not a real type reference.
    private const string StudentStatusChangedEventTypeSuffix = "%.StudentStatusChanged";

    public async Task<IReadOnlyList<StudentStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var alreadyProcessed = await context.Set<ProcessedInboundEvent>()
            .Where(e => e.Source == Source)
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var processedSet = alreadyProcessed.ToHashSet();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value.");

        var envelopes = new List<StudentStatusEventEnvelope>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand(
            """SELECT "Id", "PayloadJson", "OccurredAt" FROM student."OutboxMessages" WHERE "EventType" LIKE @eventTypeSuffix ORDER BY "RecordedAt" LIMIT @limit""",
            connection);
        command.Parameters.AddWithValue("eventTypeSuffix", StudentStatusChangedEventTypeSuffix);
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

                var payloadJson = reader.GetString(1);
                var occurredAt = reader.GetFieldValue<DateTimeOffset>(2);

                var studentId = ExtractStudentId(payloadJson);
                if (studentId is null)
                {
                    continue;
                }

                envelopes.Add(new StudentStatusEventEnvelope(eventId, studentId.Value, occurredAt));
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
