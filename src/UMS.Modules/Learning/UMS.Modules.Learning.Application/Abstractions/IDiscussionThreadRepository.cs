using UMS.Modules.Learning.Domain.Discussions;

namespace UMS.Modules.Learning.Application.Abstractions;

public interface IDiscussionThreadRepository
{
    public Task<DiscussionThread?> GetByIdAsync(DiscussionThreadId id, CancellationToken cancellationToken = default);

    public Task<DiscussionThread?> GetByPostIdAsync(DiscussionPostId postId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<DiscussionThread>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    public void Add(DiscussionThread thread);
}
