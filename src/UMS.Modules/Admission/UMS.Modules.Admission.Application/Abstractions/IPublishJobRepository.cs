using UMS.Modules.Admission.Domain.Publishing;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface IPublishJobRepository
{
    public Task<PublishJob?> GetByIdAsync(PublishJobId id, CancellationToken cancellationToken = default);

    public Task<PublishJob?> GetByAdmissionResultIdAsync(Guid admissionResultId, CancellationToken cancellationToken = default);

    /// <summary>Any PublishJob still mid-batch - polled by <c>PublishJobRelayWorker</c> (design-decisions.md's resumable-checkpoint mechanism).</summary>
    public Task<IReadOnlyList<PublishJobId>> GetActiveAsync(int batchSize, CancellationToken cancellationToken = default);

    public void Add(PublishJob job);
}
