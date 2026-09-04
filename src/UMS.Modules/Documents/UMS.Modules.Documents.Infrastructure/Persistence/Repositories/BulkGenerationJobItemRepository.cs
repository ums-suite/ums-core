using Microsoft.EntityFrameworkCore;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.BulkJobs;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Repositories;

internal sealed class BulkGenerationJobItemRepository(DocumentsDbContext context) : IBulkGenerationJobItemRepository
{
    public void AddRange(IReadOnlyCollection<BulkGenerationJobItem> items) => context.BulkGenerationJobItems.AddRange(items);

    public async Task<IReadOnlyList<BulkGenerationJobItem>> GetUnresolvedBatchAsync(BulkGenerationJobId jobId, int batchSize, CancellationToken cancellationToken = default) =>
        await context.BulkGenerationJobItems
            .Where(i => i.JobId == jobId && i.Status != BulkGenerationJobItemStatus.Completed && i.Status != BulkGenerationJobItemStatus.DeadLettered)
            .OrderBy(i => i.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountUnresolvedAsync(BulkGenerationJobId jobId, CancellationToken cancellationToken = default) =>
        context.BulkGenerationJobItems.CountAsync(
            i => i.JobId == jobId && i.Status != BulkGenerationJobItemStatus.Completed && i.Status != BulkGenerationJobItemStatus.DeadLettered,
            cancellationToken);
}
