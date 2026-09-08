using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Alumni.Application.Jobs;

namespace UMS.Workers.Alumni;

/// <summary>
/// ALM-6: the `expires_at &lt;= now AND status = Published` scan-and-transition (requirement-spec.md
/// §2.3) - mirrors Content's own <c>NoticeSchedulingSweepWorker</c> exactly (design-decisions.md item
/// 5). NO distributed lock/lease - safe to run from multiple concurrent <c>UMS.Workers</c> replicas
/// because <see cref="JobPostingExpiryService"/>'s own optimistic-concurrency write is what makes a
/// redundant concurrent tick harmless.
/// </summary>
public sealed class JobPostingExpirySweepWorker(IServiceScopeFactory scopeFactory, ILogger<JobPostingExpirySweepWorker> logger) : BackgroundService
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
                var service = scope.ServiceProvider.GetRequiredService<JobPostingExpiryService>();
                var expired = await service.ExpireDueAsync(BatchSize, stoppingToken).ConfigureAwait(false);

                if (expired > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Alumni JobPosting expiry sweep: expired {Expired} posting(s).", expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Alumni JobPosting expiry sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
