using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Domain.DashboardMetrics;

namespace UMS.Modules.Reporting.Application.Common;

/// <summary>
/// RPT-1: the shared, minimal per-dashboard-family scheduled-job shape every dashboard's own
/// refresh service extends - NOT a generic job-scheduling engine (this codebase's own convention
/// is "one bespoke BackgroundService per concern," see <c>UMS.Workers.Finance.StuckPaymentSweepWorker</c>
/// et al. - a single mega-scheduler would be premature abstraction for six jobs whose only shared
/// shape is "acquire a lease, compute a payload, upsert one DashboardMetric row").
///
/// <para>
/// <b>RPT-1 lease mechanism:</b> <see cref="IMetricRefreshLease"/> is acquired BEFORE any work
/// starts; a failed acquisition means a previous run's lease is still held, and this tick is
/// skipped entirely - no queue, no immediate retry (edge-cases.md "A scheduled MetricRefreshJob
/// still running when the next scheduled run fires").
/// </para>
///
/// <para>
/// <b>RPT-10 refresh-failure handling:</b> <see cref="ComputePayloadJsonAsync"/> is retried up to
/// <see cref="MaxAttempts"/> times with backoff (ADR-0014's shared retry pattern - bounded, not
/// indefinite, so one persistently-unavailable source module cannot starve this job's worker
/// capacity forever). On exhaustion, <see cref="DashboardMetric.RecordFailedRefresh"/> is called -
/// which, by the aggregate's own construction, cannot touch the previously-computed payload/
/// <c>data_as_of</c> - and a structured error-level log line stands in for
/// <c>DashboardMetricRefreshFailed</c> (Reporting is not in the 100%-trace/paging alerting tier per
/// <c>ums-conventions.md</c> - no paging integration exists in this codebase to hook into).
/// </para>
///
/// <para>
/// <b>RPT-2/design-decisions.md "Snapshot-Timestamp Pinning":</b> <see cref="IClock.UtcNow"/> is
/// captured exactly ONCE, before the first source-module call, and used as both the eventual
/// <c>data_as_of</c> AND the retry loop's own timestamp - every constituent read a
/// <see cref="ComputePayloadJsonAsync"/> override makes happens within this one run's short wall-
/// clock window, bounding snapshot skew by the run's own duration (this build's documented
/// no-<c>asOf</c>-parameter simplification - see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s
/// own remarks).
/// </para>
/// </summary>
public abstract class MetricRefreshJobBase(
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger logger)
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan[] BackoffDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    /// <summary>The stable <see cref="DashboardMetric"/> key this job owns, e.g. <c>"academic-dashboard"</c>.</summary>
    protected abstract string MetricKey { get; }

    protected abstract string DisplayName { get; }

    /// <summary>Set well above this job's own expected worst-case runtime (design-decisions.md's own residual note: too short risks a legitimate slow run's lease being stolen, recreating the exact interleaving this mechanism exists to prevent).</summary>
    protected abstract TimeSpan LeaseTtl { get; }

    /// <summary>Calls whichever source module(s)' public reporting-query contract(s) this dashboard needs and returns the already-aggregated payload, serialized. Never a raw cross-schema join (requirement-spec.md §4).</summary>
    protected abstract Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken);

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await using var handle = await lease.TryAcquireAsync(MetricKey, LeaseTtl, cancellationToken).ConfigureAwait(false);
        if (handle is null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Metric refresh for {MetricKey} skipped - a previous run's lease is still held.", MetricKey);
            }

            return;
        }

        var asOf = clock.UtcNow;
        string? payloadJson = null;
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts && payloadJson is null; attempt++)
        {
            try
            {
                payloadJson = await ComputePayloadJsonAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
                logger.LogWarning(ex, "Metric refresh for {MetricKey}: attempt {Attempt}/{MaxAttempts} failed.", MetricKey, attempt, MaxAttempts);
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(BackoffDelays[Math.Min(attempt - 1, BackoffDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
                }
            }
        }

        var metric = await metrics.GetByKeyAsync(MetricKey, cancellationToken).ConfigureAwait(false);
        if (metric is null)
        {
            metric = DashboardMetric.Create(MetricKey, DisplayName, asOf);
            metrics.Add(metric);
        }

        if (payloadJson is not null)
        {
            metric.RecordSuccessfulRefresh(payloadJson, asOf);
        }
        else
        {
            var errorMessage = lastError?.Message ?? "Unknown failure - no source-module data could be computed.";
            metric.RecordFailedRefresh(errorMessage, asOf);

            // Stands in for the DashboardMetricRefreshFailed alert (§5 Observability: non-paging
            // tier) - a distinguishable log level, not a real paging integration.
            logger.LogError(lastError, "DashboardMetricRefreshFailed: {MetricKey} exhausted its retry budget - the previous value and data_as_of are retained untouched.", MetricKey);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
