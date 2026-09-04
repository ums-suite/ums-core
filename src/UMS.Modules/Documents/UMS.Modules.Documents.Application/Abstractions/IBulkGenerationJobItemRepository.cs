using UMS.Modules.Documents.Domain.BulkJobs;

namespace UMS.Modules.Documents.Application.Abstractions;

public interface IBulkGenerationJobItemRepository
{
    public void AddRange(IReadOnlyCollection<BulkGenerationJobItem> items);

    /// <summary>edge-cases.md's resumability decision: items not yet <see cref="BulkGenerationJobItemStatus.Completed"/> or <see cref="BulkGenerationJobItemStatus.DeadLettered"/> - a resumed job re-queries this directly rather than re-deriving progress.</summary>
    public Task<IReadOnlyList<BulkGenerationJobItem>> GetUnresolvedBatchAsync(BulkGenerationJobId jobId, int batchSize, CancellationToken cancellationToken = default);

    public Task<int> CountUnresolvedAsync(BulkGenerationJobId jobId, CancellationToken cancellationToken = default);
}
