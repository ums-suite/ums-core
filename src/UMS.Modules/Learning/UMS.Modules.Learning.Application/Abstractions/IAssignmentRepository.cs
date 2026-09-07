using UMS.Modules.Learning.Domain.Assignments;

namespace UMS.Modules.Learning.Application.Abstractions;

public interface IAssignmentRepository
{
    public Task<Assignment?> GetByIdAsync(AssignmentId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Assignment>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    /// <summary>LRN-9's <c>hardCloseAt</c> sweep: every still-Published Assignment whose hard close is already in the past, so the worker can close it and re-enqueue any missing PlagiarismCheck.</summary>
    public Task<IReadOnlyList<Assignment>> GetPublishedPastHardCloseAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    public void Add(Assignment assignment);
}
