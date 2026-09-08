using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Infrastructure.Persistence;
using UMS.Modules.Alumni.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Alumni.Infrastructure.CrossModule;

/// <summary>
/// ALM-1: verified directly against Research's own <c>FacultyOutboxEventSource</c> (the most recent,
/// correct precedent for this exact cross-module outbox-polling shape - itself mirroring Library's/
/// Hostel's own Student-outbox pollers) - Student's own <c>StudentDbContext</c> uses EF-default
/// PascalCase naming (no explicit <c>OutboxMessageConfiguration</c>), so the physical table is
/// <c>student."OutboxMessages"</c>, quoted PascalCase columns, full-CLR-type-name <c>EventType</c>
/// values - matched by a <c>LIKE</c> suffix on <c>StudentGraduated</c>, extracting
/// <c>StudentId</c>/<c>OccurredAt</c> from the row itself (Student's own event carries no other
/// payload fields - see <c>Alumnus</c>'s own remarks on how graduation year/program/department are
/// actually resolved).
/// </summary>
internal sealed class StudentOutboxEventSource(AlumniDbContext context, IConfiguration configuration, ILogger<StudentOutboxEventSource> logger) : IStudentGraduatedEventSource
{
    private const string Source = "student";
    private const string EventTypeSuffix = "%.StudentGraduated";

    public async Task<IReadOnlyList<StudentGraduatedEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var processedSet = (await context.Set<ProcessedInboundEvent>()
            .Where(e => e.Source == Source)
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value.");

        var envelopes = new List<StudentGraduatedEventEnvelope>();

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

                envelopes.Add(new StudentGraduatedEventEnvelope(eventId, studentId.Value, reader.GetFieldValue<DateTimeOffset>(2)));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogDebug("Student outbox table does not exist - reporting zero unprocessed StudentGraduated events.");
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
        return document.RootElement.TryGetProperty("StudentId", out var idElement) && idElement.TryGetGuid(out var studentId)
            ? studentId
            : null;
    }
}
