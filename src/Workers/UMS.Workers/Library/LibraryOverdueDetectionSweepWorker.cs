using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Loans;

namespace UMS.Workers.Library;

/// <summary>LIB-10: the computed-Overdue detection sweep - requirement-spec.md §3/§4.</summary>
public sealed class LibraryOverdueDetectionSweepWorker(IServiceScopeFactory scopeFactory, ILogger<LibraryOverdueDetectionSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 200;
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<LoanOverdueDetectionService>();
                var notified = await service.DetectAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                if (notified > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Library overdue-detection sweep: raised {Count} new LoanOverdue notice(s).", notified);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Library overdue-detection sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
