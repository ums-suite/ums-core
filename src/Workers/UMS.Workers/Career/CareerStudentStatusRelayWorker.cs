using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Career.Application.StudentGraduation;

namespace UMS.Workers.Career;

/// <summary>
/// CAR-16: polls Student's own outbox for `StudentGraduated`/`StudentStatusChanged` and hands every
/// envelope to `StudentStatusEventConsumerService` - mirrors Alumni's own
/// `AlumniStudentGraduatedRelayWorker` shape exactly. design-decisions.md "Student-Graduation
/// Boundary for In-Flight Career Activity": the consumer itself performs NO mutation to any
/// `CareerApplication` - this worker's only job is to keep the ack cursor moving so the underlying
/// `student."OutboxMessages"` poll query stays bounded.
/// </summary>
public sealed class CareerStudentStatusRelayWorker(IServiceScopeFactory scopeFactory, ILogger<CareerStudentStatusRelayWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<StudentStatusEventConsumerService>();
                var processed = await service.ConsumeUnprocessedAsync(BatchSize, stoppingToken).ConfigureAwait(false);

                if (processed > 0 && logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug("Career Student status relay: acknowledged {Processed} event(s).", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Career Student status relay: unexpected failure while processing pending events.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
