using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.Application.Abstractions;

public interface IRegulatoryReportDefinitionRepository
{
    public Task<RegulatoryReportDefinition?> GetByIdAsync(RegulatoryReportDefinitionId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<RegulatoryReportDefinition>> GetPageAsync(int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(CancellationToken cancellationToken = default);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    public void Add(RegulatoryReportDefinition definition);
}
