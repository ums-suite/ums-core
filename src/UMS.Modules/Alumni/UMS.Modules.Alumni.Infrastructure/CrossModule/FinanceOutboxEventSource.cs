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
/// ALM-9: verified directly against Admission's own <c>FinanceOutboxEventSource</c> (the precedent
/// this task names explicitly) - Finance's own outbox uses snake_case (<c>finance.outbox_messages</c>,
/// short event names <c>PaymentSucceeded</c>/<c>PaymentFailed</c>) - a DIFFERENT wire shape from
/// Student's/Faculty's PascalCase (the third distinct outbox wire shape in this codebase). Finance
/// itself never calls back into Alumni; acknowledgement is tracked in Alumni's OWN
/// <c>processed_inbound_events</c> table, never a write into Finance's schema.
/// </summary>
internal sealed class FinanceOutboxEventSource(AlumniDbContext context, IConfiguration configuration, ILogger<FinanceOutboxEventSource> logger) : IFinancePaymentEventSource
{
    private const string Source = "finance";
    private static readonly string[] EventTypes = ["PaymentSucceeded", "PaymentFailed"];

    public async Task<IReadOnlyList<FinancePaymentEventEnvelope>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var processedSet = (await context.Set<ProcessedInboundEvent>()
            .Where(e => e.Source == Source)
            .Select(e => e.EventId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

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
        context.Set<ProcessedInboundEvent>().Add(new ProcessedInboundEvent(eventId, Source, DateTimeOffset.UtcNow));
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Guid? ExtractInvoiceId(string payloadJson)
    {
        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty("InvoiceId", out var invoiceIdElement) && invoiceIdElement.TryGetGuid(out var invoiceId)
            ? invoiceId
            : null;
    }
}
