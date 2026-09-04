using UMS.Modules.Documents.Domain.BulkJobs;

namespace UMS.Modules.Documents.Application.Abstractions;

public interface IBulkGenerationJobRepository
{
    public void Add(BulkGenerationJob job);

    public Task<BulkGenerationJob?> GetByIdAsync(BulkGenerationJobId id, CancellationToken cancellationToken = default);
}
