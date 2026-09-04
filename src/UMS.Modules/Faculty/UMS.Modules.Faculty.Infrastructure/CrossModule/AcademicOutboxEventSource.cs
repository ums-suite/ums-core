using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Infrastructure.Persistence;

namespace UMS.Modules.Faculty.Infrastructure.CrossModule;

/// <summary>
/// FAC-4: reads Academic's own outbox table (<c>academic.outbox_messages</c>, the shared
/// <c>UMS.Shared.Outbox.OutboxMessage</c> row shape every module's own outbox follows) directly by
/// schema-qualified name, via a plain Npgsql connection rather than an EF <c>DbContext</c> -
/// deliberately, since a `DbContext` typed to Academic's schema would require a project reference
/// into a module that doesn't exist yet, and even once it does, Faculty must never reference
/// <c>UMS.Modules.Academic.*</c> internals (module-boundaries.md, ADR-0002). Reading a plain
/// Postgres table by name, sharing only the one physical database (ADR-0001) every module already
/// shares, is not a forbidden cross-module reference the way a project/assembly reference would
/// be - see <see cref="IInstructorAssignmentEventSource"/>'s own remarks for why this is a
/// first-of-its-kind pattern in this codebase, invented for this ticket rather than copied from an
/// existing precedent.
///
/// <para>
/// Acknowledgement never writes back into Academic's table (that would cross module ownership the
/// other way) - instead, every event id this reads is recorded into Faculty's own
/// <c>processed_inbound_events</c> table, and future reads exclude anything already recorded
/// there.
/// </para>
///
/// <para>
/// <b>Known gap, documented rather than glossed over:</b> Academic (release/
/// DEVELOPMENT_PLAN.md Flow #12) does not exist yet, so <c>academic.outbox_messages</c> does not
/// exist in any environment this code runs in today. <see cref="GetUnprocessedAsync"/> catches
/// Postgres' "undefined table" error (<c>42P01</c>) and returns an empty list rather than
/// crashing the worker loop, logged once at Debug rather than spamming Warning/Error every poll -
/// this mechanism is built and integration-tested against a fixture table matching the real shape
/// (see the integration test suite), but is not exercised against real Academic-published events
/// until Flow #12 lands, mirroring how Organization's own former <c>IFacultyEmploymentChecker</c>
/// stub was built and registered well before this module existed to satisfy it for real.
/// </para>
/// </summary>
internal sealed class AcademicOutboxEventSource(FacultyDbContext context, IConfiguration configuration, ILogger<AcademicOutboxEventSource> logger) : IInstructorAssignmentEventSource
{
    private static readonly string[] EventTypes = ["InstructorAssigned", "InstructorUnassigned"];

    public async Task<IReadOnlyList<InstructorAssignmentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var alreadyProcessed = await context.ProcessedInboundEvents
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var processedSet = alreadyProcessed.ToHashSet();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing required 'ConnectionStrings:Postgres' configuration value.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Requests a bigger page than batchSize since some rows will be filtered out client-side
        // as already-processed (no cross-schema JOIN against Faculty's own tracking table).
        await using var command = new NpgsqlCommand(
            "SELECT id, event_type, payload_json, occurred_at FROM academic.outbox_messages WHERE event_type = ANY(@eventTypes) ORDER BY recorded_at LIMIT @limit",
            connection);
        command.Parameters.AddWithValue("eventTypes", EventTypes);
        command.Parameters.AddWithValue("limit", batchSize * 4);

        var envelopes = new List<InstructorAssignmentEventEnvelope>();
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

                envelopes.Add(new InstructorAssignmentEventEnvelope(eventId, reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3)));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogDebug("Academic outbox table does not exist yet (Academic - Flow #12 - is not built) - reporting zero unprocessed InstructorAssigned/InstructorUnassigned events.");
            return [];
        }

        return envelopes;
    }

    public async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        context.ProcessedInboundEvents.Add(new ProcessedInboundEvent(eventId, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
