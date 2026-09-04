using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.BulkJobs;

/// <summary>DOC-7: job status/progress query (requirement-spec.md documents §6).</summary>
public sealed class BulkJobStatusService(IBulkGenerationJobRepository jobs)
{
    public async Task<Result<BulkGenerationJobDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetByIdAsync(new BulkGenerationJobId(id), cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Error.NotFound("bulk_generation_job.not_found", $"No BulkGenerationJob exists with id '{id}'.");
        }

        return BulkGenerationJobDto.FromDomain(job);
    }
}
