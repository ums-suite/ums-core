using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Infrastructure.Persistence;
using UMS.Modules.Admission.Infrastructure.Persistence.Inbox;

namespace UMS.Modules.Admission.Infrastructure.CrossModule;

/// <summary>
/// design-decisions.md "Idempotency for Application Submission (Including Duplicate Payment-
/// Webhook Delivery)": reads Finance's own outbox table (<c>finance.outbox_messages</c>) directly
/// by schema-qualified name, via a plain Npgsql connection - mirroring Faculty's own
/// <c>AcademicOutboxEventSource</c> pattern exactly (see that class's own remarks for why this is
/// the established mechanism for "consumer reacts to producer's event" beyond Notifications-
/// specific fan-out). Finance itself never calls into Admission (module-boundaries.md: "Finance
/// intentionally has no outgoing domain dependency") - this is a READ-only poll, acknowledged in
/// Admission's own <c>processed_inbound_events</c> table, never a write into Finance's schema.
///
/// <para>
/// Unlike Faculty's own build-time posture (Academic did not exist yet), Finance already exists and
/// is merged - <c>finance.outbox_messages</c> is real in every environment this code runs in. The
/// "undefined table" defensive catch is kept anyway, at negligible cost, as the same safety net
/// every cross-module outbox reader in this codebase now carries by convention.
/// </para>
/// </summary>
internal sealed class FinanceOutboxEventSource(AdmissionDbContext context, IConfiguration configuration, ILogger<FinanceOutboxEventSource> logger) : IFinancePaymentEventSource
{
    private static readonly string[] EventTypes = ["PaymentSucceeded", "PaymentFailed"];

    public async Task<IReadOnlyList<FinancePaymentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
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

        await using var command = new NpgsqlCommand(
            "SELECT id, event_type, payload_json, occurred_at FROM finance.outbox_messages WHERE event_type = ANY(@eventTypes) ORDER BY recorded_at LIMIT @limit",
            connection);
        command.Parameters.AddWithValue("eventTypes", EventTypes);
        command.Parameters.AddWithValue("limit", batchSize * 4);

        var envelopes = new List<FinancePaymentEventEnvelope>();
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

                var eventType = reader.GetString(1);
                var payloadJson = reader.GetString(2);
                var occurredAt = reader.GetFieldValue<DateTimeOffset>(3);
                var invoiceId = ExtractInvoiceId(payloadJson);
                if (invoiceId is null)
                {
                    continue;
                }

                envelopes.Add(new FinancePaymentEventEnvelope(eventId, eventType, invoiceId.Value, occurredAt));
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            logger.LogDebug("Finance outbox table does not exist - reporting zero unprocessed payment events.");
            return [];
        }

        return envelopes;
    }

    public async Task MarkProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        context.ProcessedInboundEvents.Add(new ProcessedInboundEvent(eventId, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Guid? ExtractInvoiceId(string payloadJson)
    {
        using var document = System.Text.Json.JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("InvoiceId", out var invoiceIdElement) && invoiceIdElement.TryGetGuid(out var invoiceId)
            ? invoiceId
            : null;
    }
}
