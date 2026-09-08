using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Alumni;

namespace UMS.Workers.Alumni;

/// <summary>
/// ALM-1: polls Student's own outbox for <c>StudentGraduated</c> and idempotently creates the linked
/// <c>Alumnus</c> record (requirement-spec.md §2.1) - mirrors Research's own
/// <c>ResearchFacultyStatusRelayWorker</c> shape exactly (the most recent, correct precedent for this
/// exact cross-module outbox-polling relay worker pattern).
/// </summary>
public sealed class AlumniStudentGraduatedRelayWorker(IServiceScopeFactory scopeFactory, ILogger<AlumniStudentGraduatedRelayWorker> logger) : BackgroundService
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
                logger.LogError(ex, "Alumni StudentGraduated relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IStudentGraduatedEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<AlumnusService>();

        var envelopes = await eventSource.GetUnprocessedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var envelope in envelopes)
        {
            try
            {
                var result = await service.CreateFromStudentGraduationAsync(envelope.StudentId, envelope.OccurredAt, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    // Genuinely transient (Student's own record not yet resolvable via the shared
                    // read contract) - leave unmarked so the next poll retries, never dropped.
                    logger.LogWarning("Alumni StudentGraduated relay: could not create Alumnus for StudentId {StudentId} yet ({Error}) - will retry next poll.", envelope.StudentId, result.Error!.Code);
                    continue;
                }

                await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alumni StudentGraduated relay: unhandled failure applying event {EventId}.", envelope.EventId);
            }
        }
    }
}
