using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.BulkJobs;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeBulkGenerationJobItemRepository : IBulkGenerationJobItemRepository
{
    public List<BulkGenerationJobItem> Items { get; } = [];

    public void AddRange(IReadOnlyCollection<BulkGenerationJobItem> items) => Items.AddRange(items);

    public Task<IReadOnlyList<BulkGenerationJobItem>> GetUnresolvedBatchAsync(BulkGenerationJobId jobId, int batchSize, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<BulkGenerationJobItem>>(Items
            .Where(i => i.JobId == jobId && i.Status != BulkGenerationJobItemStatus.Completed && i.Status != BulkGenerationJobItemStatus.DeadLettered)
            .Take(batchSize)
            .ToList());

    public Task<int> CountUnresolvedAsync(BulkGenerationJobId jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(i => i.JobId == jobId && i.Status != BulkGenerationJobItemStatus.Completed && i.Status != BulkGenerationJobItemStatus.DeadLettered));
}
