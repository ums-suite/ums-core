using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.BulkJobs;

namespace UMS.Modules.Documents.UnitTests.TestDoubles;

public sealed class FakeBulkGenerationJobRepository : IBulkGenerationJobRepository
{
    private readonly List<BulkGenerationJob> _jobs = [];

    public void Add(BulkGenerationJob job) => _jobs.Add(job);

    public Task<BulkGenerationJob?> GetByIdAsync(BulkGenerationJobId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_jobs.FirstOrDefault(j => j.Id == id));
}
