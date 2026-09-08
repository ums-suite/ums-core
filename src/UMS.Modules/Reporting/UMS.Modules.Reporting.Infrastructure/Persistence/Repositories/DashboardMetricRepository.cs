using Microsoft.EntityFrameworkCore;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Domain.DashboardMetrics;

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class DashboardMetricRepository(ReportingDbContext context) : IDashboardMetricRepository
{
    public Task<DashboardMetric?> GetByKeyAsync(string metricKey, CancellationToken cancellationToken = default) =>
        context.DashboardMetrics.FirstOrDefaultAsync(m => m.Id == metricKey, cancellationToken);

    public async Task<IReadOnlyList<DashboardMetric>> GetByKeysAsync(IReadOnlyCollection<string> metricKeys, CancellationToken cancellationToken = default) =>
        await context.DashboardMetrics.Where(m => metricKeys.Contains(m.Id)).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(DashboardMetric metric) => context.DashboardMetrics.Add(metric);
}
