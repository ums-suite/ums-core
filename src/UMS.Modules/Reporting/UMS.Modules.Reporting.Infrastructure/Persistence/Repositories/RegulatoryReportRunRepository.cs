using Microsoft.EntityFrameworkCore;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class RegulatoryReportRunRepository(ReportingDbContext context) : IRegulatoryReportRunRepository
{
    public Task<RegulatoryReportRun?> GetByIdAsync(RegulatoryReportRunId id, CancellationToken cancellationToken = default) =>
        context.RegulatoryReportRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<RegulatoryReportRun?> FindInFlightByParametersHashAsync(string parametersHash, CancellationToken cancellationToken = default) =>
        context.RegulatoryReportRuns
            .Where(r => r.ParametersHash == parametersHash && r.Status != RegulatoryReportRunStatus.Completed && r.Status != RegulatoryReportRunStatus.Failed)
            .OrderByDescending(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<RegulatoryReportRun>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default) =>
        await context.RegulatoryReportRuns
            .Where(r => r.Status == RegulatoryReportRunStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(RegulatoryReportRun run) => context.RegulatoryReportRuns.Add(run);
}
