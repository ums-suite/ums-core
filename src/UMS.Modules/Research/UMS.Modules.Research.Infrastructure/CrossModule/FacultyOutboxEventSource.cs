using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Infrastructure.Persistence;
using UMS.Modules.Research.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Research.Infrastructure.CrossModule;

/// <summary>
/// RES-5: verified directly against Library's own <c>FacultyOutboxEventSource</c> (the most recent,
/// correct precedent for this exact cross-module outbox-polling shape) - Faculty's own
/// <c>FacultyDbContext</c> uses EF-default PascalCase naming (no explicit
/// <c>OutboxMessageConfiguration</c>), so the physical table is <c>faculty."OutboxMessages"</c>,
/// quoted PascalCase columns, full-CLR-type-name <c>EventType</c> values - matched by a
/// <c>LIKE</c> suffix on <c>FacultyMemberStatusChanged</c>, extracting <c>FacultyMemberId</c> from
/// the payload. Deliberately NOT Finance's snake_case shape, per the same verification the task
/// itself calls out.
/// </summary>
internal sealed class FacultyOutboxEventSource(ResearchDbContext context, IConfiguration configuration, ILogger<FacultyOutboxEventSource> logger) : IFacultyStatusEventSource
{
    private const string Source = "faculty";
    private const string EventTypeSuffix = "%.FacultyMemberStatusChanged";

    public async Task<IReadOnlyList<FacultyStatusEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var processedSet = (await context.Set<ProcessedInboundEvent>()
            .Where(e => e.Source == Source)
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value.");

        var envelopes = new List<FacultyStatusEventEnvelope>();

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = new NpgsqlCommand(
            """SELECT "Id", "PayloadJson", "OccurredAt" FROM faculty."OutboxMessages" WHERE "EventType" LIKE @eventTypeSuffix ORDER BY "RecordedAt" LIMIT @limit""",
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

                var facultyMemberId = ExtractFacultyMemberId(reader.GetString(1));
                if (facultyMemberId is null)
                {
                    continue;
                }

                envelopes.Add(new FacultyStatusEventEnvelope(eventId, facultyMemberId.Value, reader.GetFieldValue<DateTimeOffset>(2)));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogDebug("Faculty outbox table does not exist - reporting zero unprocessed status-change events.");
            return [];
        }

        return envelopes;
    }

    public async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        context.Set<ProcessedInboundEvent>().Add(new ProcessedInboundEvent(eventId, Source, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Guid? ExtractFacultyMemberId(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("FacultyMemberId", out var idElement) && idElement.TryGetGuid(out var facultyMemberId)
            ? facultyMemberId
            : null;
    }
}
