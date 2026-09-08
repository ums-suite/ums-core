using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.Application.Abstractions;

public interface IRegulatoryReportRunRepository
{
    public Task<RegulatoryReportRun?> GetByIdAsync(RegulatoryReportRunId id, CancellationToken cancellationToken = default);

    /// <summary>RPT-1's per-job overlap prevention has a courtesy analog here (RPT-15/edge-cases.md "concurrent identical run requests"): the most recent non-terminal run sharing the same <see cref="RegulatoryReportRun.ParametersHash"/>, if any - surfaced as an informational notice, never a block.</summary>
    public Task<RegulatoryReportRun?> FindInFlightByParametersHashAsync(string parametersHash, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<RegulatoryReportRun>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    public void Add(RegulatoryReportRun run);
}
