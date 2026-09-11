using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.Internships;

namespace UMS.Workers.Career;

/// <summary>
/// CAR-3: the `application_deadline &lt;= now AND status = ApplicationsOpen` scan-and-transition
/// (requirement-spec.md §2.2) - mirrors Alumni's own `JobPostingExpirySweepWorker` exactly. NO
/// distributed lock/lease - safe to run from multiple concurrent `UMS.Workers` replicas because
/// `InternshipDeadlineSweepService`'s own optimistic-concurrency write makes a redundant concurrent
/// tick harmless.
/// </summary>
public sealed class InternshipDeadlineSweepWorker(IServiceScopeFactory scopeFactory, ILogger<InternshipDeadlineSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<InternshipDeadlineSweepService>();
                var closed = await service.CloseDueAsync(BatchSize, stoppingToken).ConfigureAwait(false);

                if (closed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Career Internship deadline sweep: closed {Closed} posting(s).", closed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Career Internship deadline sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
