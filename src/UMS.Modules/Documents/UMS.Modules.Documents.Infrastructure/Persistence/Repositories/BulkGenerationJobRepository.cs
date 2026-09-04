using Microsoft.EntityFrameworkCore;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.BulkJobs;

namespace UMS.Modules.Documents.Infrastructure.Persistence.Repositories;

internal sealed class BulkGenerationJobRepository(DocumentsDbContext context) : IBulkGenerationJobRepository
{
    public void Add(BulkGenerationJob job) => context.BulkGenerationJobs.Add(job);

    public Task<BulkGenerationJob?> GetByIdAsync(BulkGenerationJobId id, CancellationToken cancellationToken = default) =>
        context.BulkGenerationJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
}
