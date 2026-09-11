using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Infrastructure.Persistence;
using UMS.Modules.Career.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Career.Infrastructure.CrossModule;

/// <summary>
/// CAR-16: verified directly against Alumni's own <c>StudentOutboxEventSource</c> (the most recent,
/// correct precedent for this exact cross-module outbox-polling shape) - Student's own
/// <c>StudentDbContext</c> uses EF-default PascalCase naming (no explicit <c>OutboxMessageConfiguration</c>),
/// so the physical table is <c>student."OutboxMessages"</c>, quoted PascalCase columns, full-CLR-type-name
/// <c>EventType</c> values - matched by a <c>LIKE</c> suffix on either <c>StudentGraduated</c> or
/// <c>StudentStatusChanged</c>, extracting <c>StudentId</c>/<c>OccurredAt</c> from the row itself.
///
/// <para>
/// design-decisions.md "Student-Graduation Boundary for In-Flight Career Activity": both event types
/// are polled identically and handed to <c>StudentStatusEventConsumerService</c>, which performs NO
/// mutation to any existing `CareerApplication` - see that class's own remarks.
/// </para>
/// </summary>
internal sealed class StudentOutboxEventSource(CareerDbContext context, IConfiguration configuration, ILogger<StudentOutboxEventSource> logger) : IStudentStatusEventSource
{
    private const string Source = "student";
    private static readonly string[] EventTypeSuffixes = ["%.StudentGraduated", "%.StudentStatusChanged"];

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

        foreach (var suffix in EventTypeSuffixes)
        {
            if (envelopes.Count >= batchSize)
            {
                break;
            }

            await using var command = new NpgsqlCommand(
                """SELECT "Id", "EventType", "PayloadJson", "OccurredAt" FROM student."OutboxMessages" WHERE "EventType" LIKE @eventTypeSuffix ORDER BY "RecordedAt" LIMIT @limit""",
                connection);
            command.Parameters.AddWithValue("eventTypeSuffix", suffix);
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

                    var fullEventType = reader.GetString(1);
                    var eventName = suffix.TrimStart('%', '.');
                    var studentId = ExtractStudentId(reader.GetString(2));
                    if (studentId is null)
                    {
                        continue;
                    }

                    envelopes.Add(new StudentStatusEventEnvelope(eventId, eventName, studentId.Value, reader.GetFieldValue<DateTimeOffset>(3)));
                }
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
            {
                logger.LogDebug("Student outbox table does not exist - reporting zero unprocessed Student status events.");
                return [];
            }
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
