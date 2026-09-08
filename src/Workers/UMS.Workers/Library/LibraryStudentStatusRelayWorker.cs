using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Application.Loans;

namespace UMS.Workers.Library;

/// <summary>LIB-16: polls Student's own outbox for <c>StudentStatusChanged</c>, flags any open Loan for recall-notice review on a no-longer-Active Student.</summary>
public sealed class LibraryStudentStatusRelayWorker(IServiceScopeFactory scopeFactory, ILogger<LibraryStudentStatusRelayWorker> logger) : BackgroundService
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
                logger.LogError(ex, "Library Student-status relay: unexpected failure while processing pending StudentStatusChanged events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IStudentStatusEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<LoanReviewFlagService>();

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
                logger.LogError(ex, "Library Student-status relay: unhandled failure applying event {EventId}.", envelope.EventId);
            }
        }
    }
}
