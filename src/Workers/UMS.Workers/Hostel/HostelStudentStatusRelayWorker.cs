using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Allocations;

namespace UMS.Workers.Hostel;

/// <summary>HOS-17: Hostel is the FIRST cross-module consumer of Student's own outbox - polls for <c>StudentStatusChanged</c>, flags any open Allocation for officer review on Suspended/Graduated.</summary>
public sealed class HostelStudentStatusRelayWorker(IServiceScopeFactory scopeFactory, ILogger<HostelStudentStatusRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Hostel Student-status relay: unexpected failure while processing pending StudentStatusChanged events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IStudentStatusEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<AllocationReviewFlagService>();

        var envelopes = await eventSource.GetUnprocessedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var envelope in envelopes)
        {
            try
            {
                await service.ApplyStudentStatusChangeAsync(envelope.StudentId, $"StudentStatusChanged:{envelope.EventId}", cancellationToken).ConfigureAwait(false);
                await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Hostel Student-status relay: unhandled failure applying event {EventId}.", envelope.EventId);
            }
        }
    }
}
