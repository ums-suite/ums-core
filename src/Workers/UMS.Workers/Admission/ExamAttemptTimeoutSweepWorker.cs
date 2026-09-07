using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Admission.Application.ExamAttempts;

namespace UMS.Workers.Admission;

/// <summary>ADM-13/edge-cases.md "Time expires mid-question": the server-side timeout sweep - see <see cref="ExamAttemptTimeoutSweepService"/>'s own remarks. A short poll interval since a real applicant's clock-accuracy expectation is second-scale, not minute-scale.</summary>
public sealed class ExamAttemptTimeoutSweepWorker(IServiceScopeFactory scopeFactory, ILogger<ExamAttemptTimeoutSweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ExamAttemptTimeoutSweepService>();
                var processed = await service.SweepAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                if (processed > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Admission ExamAttempt timeout sweep: auto-submitted {Processed} attempt(s).", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Admission ExamAttempt timeout sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
