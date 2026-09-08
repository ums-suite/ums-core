using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Library.Application.Reservations;

namespace UMS.Workers.Library;

/// <summary>LIB-9: requirement-spec.md §2 - the bounded claim-window expiry sweep, mirroring Hostel's own <c>HostelGracePeriodSweepWorker</c> shape.</summary>
public sealed class LibraryReservationExpirySweepWorker(IServiceScopeFactory scopeFactory, ILogger<LibraryReservationExpirySweepWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ReservationFulfillmentService>();
                var expired = await service.ExpireClaimWindowsAsync(BatchSize, stoppingToken).ConfigureAwait(false);
                if (expired > 0 && logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Library reservation-expiry sweep: expired {Count} unclaimed Reservation offer(s).", expired);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Library reservation-expiry sweep: unexpected failure during a poll pass.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }
}
