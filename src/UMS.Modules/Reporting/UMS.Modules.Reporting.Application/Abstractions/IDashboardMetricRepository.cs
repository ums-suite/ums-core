using UMS.Modules.Reporting.Domain.DashboardMetrics;

namespace UMS.Modules.Reporting.Application.Abstractions;

public interface IDashboardMetricRepository
{
    public Task<DashboardMetric?> GetByKeyAsync(string metricKey, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DashboardMetric>> GetByKeysAsync(IReadOnlyCollection<string> metricKeys, CancellationToken cancellationToken = default);

    public void Add(DashboardMetric metric);
}
