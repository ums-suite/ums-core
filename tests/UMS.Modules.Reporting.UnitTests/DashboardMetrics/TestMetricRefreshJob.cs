using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;

namespace UMS.Modules.Reporting.UnitTests.DashboardMetrics;

/// <summary>A minimal, fully-controllable <see cref="MetricRefreshJobBase"/> subclass - lets the test drive exactly how many times/how <see cref="ComputePayloadJsonAsync"/> fails before it (optionally) succeeds, mirroring what a real dashboard's own refresh job leaves to its source-module query call.</summary>
public sealed class TestMetricRefreshJob(
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger logger,
    Queue<Func<string>> computations,
    TimeSpan? leaseTtl = null)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public int ComputeCallCount { get; private set; }

    protected override string MetricKey => "test-dashboard";

    protected override string DisplayName => "Test Dashboard";

    protected override TimeSpan LeaseTtl => leaseTtl ?? TimeSpan.FromMinutes(10);

    protected override Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        ComputeCallCount++;
        var next = computations.Dequeue();
        return Task.FromResult(next());
    }
}
