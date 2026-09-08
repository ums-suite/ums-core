using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.Grants;

namespace UMS.Workers.Research;

/// <summary>RES-5: mirrors Library's own <c>LibraryFacultyStatusRelayWorker</c> exactly - polls Faculty's own outbox for <c>FacultyMemberStatusChanged</c>, flagging any Funded/Active Grant where the departed FacultyMember holds the active PI role.</summary>
public sealed class ResearchFacultyStatusRelayWorker(IServiceScopeFactory scopeFactory, ILogger<ResearchFacultyStatusRelayWorker> logger) : BackgroundService
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
                logger.LogError(ex, "Research Faculty-status relay: unexpected failure while processing pending FacultyMemberStatusChanged events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var eventSource = scope.ServiceProvider.GetRequiredService<IFacultyStatusEventSource>();
        var service = scope.ServiceProvider.GetRequiredService<GrantPiVacancyService>();

        var envelopes = await eventSource.GetUnprocessedAsync(BatchSize, cancellationToken).ConfigureAwait(false);
        foreach (var envelope in envelopes)
        {
            try
            {
                await service.ApplyFacultyStatusChangeAsync(envelope.FacultyMemberId, $"FacultyMemberStatusChanged:{envelope.EventId}", cancellationToken).ConfigureAwait(false);
                await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Research Faculty-status relay: unhandled failure applying event {EventId}.", envelope.EventId);
            }
        }
    }
}
