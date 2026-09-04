using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Application.Exports;
using UMS.Modules.Audit.Domain.Exports;

namespace UMS.Workers.AuditExports;

/// <summary>
/// AUD-9/AUD-10: the worker side of Audit's async export feature (ADR-0014's shared outbox/worker
/// pattern) - polls Audit's own outbox for "AuditExportRequested" triggers, renders the filtered
/// result set, uploads it to object storage, and updates the <c>AuditExportRequest</c> job status.
///
/// <para>
/// <b>Known gap, not silently glossed over:</b> only <see cref="ExportFormat.Csv"/> is implemented
/// end-to-end. A <see cref="ExportFormat.Pdf"/> request is accepted by the API (AUD-9's endpoint
/// and the domain model both support it) but fails here with a clear, documented error - PDF
/// rendering was out of reach within this pass's time budget; CSV covers the same filtered data,
/// so no export request is silently dropped, it just fails with an actionable status message.
/// </para>
/// </summary>
public sealed class AuditExportRelayWorker(IServiceScopeFactory scopeFactory, ILogger<AuditExportRelayWorker> logger) : BackgroundService
{
    private const string ExportRequestedEventType = "AuditExportRequested";
    private const int BatchSize = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingExportsAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Audit export relay: unexpected failure while processing pending exports.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingExportsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxReader>();

        var messages = await outbox.GetUnprocessedAsync(ExportRequestedEventType, BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var message in messages)
        {
            await ProcessOneAsync(scope.ServiceProvider, message, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(IServiceProvider services, OutboxMessageDto message, CancellationToken cancellationToken)
    {
        var outbox = services.GetRequiredService<IOutboxReader>();
        var exportRequests = services.GetRequiredService<IAuditExportRequestRepository>();
        var entries = services.GetRequiredService<IAuditLogEntryRepository>();
        var objectStorage = services.GetRequiredService<IObjectStorage>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();

        try
        {
            var payload = JsonSerializer.Deserialize<AuditExportRequestedPayload>(message.PayloadJson)
                ?? throw new InvalidOperationException("Empty AuditExportRequested payload.");

            var request = await exportRequests.GetByIdAsync(payload.ExportRequestId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"AuditExportRequest '{payload.ExportRequestId}' referenced by outbox message '{message.Id}' no longer exists.");

            if (request.Status != ExportStatus.Pending)
            {
                // Already handled by a previous, since-crashed attempt that got as far as
                // updating the request but not yet marking the outbox message processed
                // (ADR-0014: "idempotent by construction, safe to retry") - just ack the message.
                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                return;
            }

            request.MarkProcessing();
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (request.Format != ExportFormat.Csv)
            {
                request.MarkFailed($"Export format '{request.Format}' is not yet implemented by the export relay - only Csv is currently supported.", DateTimeOffset.UtcNow);
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
                return;
            }

            var filter = JsonSerializer.Deserialize<AuditEntryFilter>(request.FilterJson) ?? new AuditEntryFilter();
            var matched = await entries.ListAllAsync(filter, cancellationToken).ConfigureAwait(false);
            var csv = AuditEntryCsvRenderer.Render(matched.Select(AuditLogEntryDto.FromDomain));

            var objectKey = $"audit-exports/{request.Id}.csv";
            await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
            {
                await objectStorage.UploadAsync(objectKey, stream, "text/csv", cancellationToken).ConfigureAwait(false);
            }

            request.MarkCompleted(objectKey, DateTimeOffset.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await outbox.MarkProcessedAsync(message.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Audit export relay: failed to process outbox message {MessageId}.", message.Id);
            await outbox.RecordFailedAttemptAsync(message.Id, ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }
}
