using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Publishing;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

internal sealed class PublishJobRepository(AdmissionDbContext context) : IPublishJobRepository
{
    public Task<PublishJob?> GetByIdAsync(PublishJobId id, CancellationToken cancellationToken = default) =>
        context.PublishJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public Task<PublishJob?> GetByAdmissionResultIdAsync(Guid admissionResultId, CancellationToken cancellationToken = default) =>
        context.PublishJobs.FirstOrDefaultAsync(j => j.AdmissionResultId == admissionResultId, cancellationToken);

    public async Task<IReadOnlyList<PublishJobId>> GetActiveAsync(int batchSize, CancellationToken cancellationToken = default) =>
        await context.PublishJobs
            .Where(j => j.Stage == PublishJobStage.WarmingCache || j.Stage == PublishJobStage.FanningOut)
            .OrderBy(j => j.StartedAt)
            .Take(batchSize)
            .Select(j => j.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(PublishJob job) => context.PublishJobs.Add(job);
}
