using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Domain.DashboardMetrics;

namespace UMS.Modules.Reporting.UnitTests.Fakes;

public sealed class FakeDashboardMetricRepository : IDashboardMetricRepository
{
    private readonly Dictionary<string, DashboardMetric> _metrics = [];

    public Task<DashboardMetric?> GetByKeyAsync(string metricKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(_metrics.GetValueOrDefault(metricKey));

    public Task<IReadOnlyList<DashboardMetric>> GetByKeysAsync(IReadOnlyCollection<string> metricKeys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DashboardMetric>>(metricKeys.Where(_metrics.ContainsKey).Select(k => _metrics[k]).ToList());

    public void Add(DashboardMetric metric) => _metrics[metric.Id] = metric;
}
