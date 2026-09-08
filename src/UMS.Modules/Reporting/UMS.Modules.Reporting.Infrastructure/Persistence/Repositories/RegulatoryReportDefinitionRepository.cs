using Microsoft.EntityFrameworkCore;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Repositories;

internal sealed class RegulatoryReportDefinitionRepository(ReportingDbContext context) : IRegulatoryReportDefinitionRepository
{
    public Task<RegulatoryReportDefinition?> GetByIdAsync(RegulatoryReportDefinitionId id, CancellationToken cancellationToken = default) =>
        context.RegulatoryReportDefinitions.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RegulatoryReportDefinition>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.RegulatoryReportDefinitions
            .OrderBy(d => d.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        context.RegulatoryReportDefinitions.CountAsync(cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default) =>
        context.RegulatoryReportDefinitions.AnyAsync(d => d.Name == name, cancellationToken);

    public void Add(RegulatoryReportDefinition definition) => context.RegulatoryReportDefinitions.Add(definition);
}
