using Microsoft.EntityFrameworkCore;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Discussions;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Repositories;

internal sealed class DiscussionThreadRepository(LearningDbContext context) : IDiscussionThreadRepository
{
    public Task<DiscussionThread?> GetByIdAsync(DiscussionThreadId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <summary>LRN-18 moderates a post by its own id; the thread is the aggregate that owns the transition, so it is loaded whole.</summary>
    public Task<DiscussionThread?> GetByPostIdAsync(DiscussionPostId postId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(t => t.Posts.Any(p => p.Id == postId), cancellationToken);

    public async Task<IReadOnlyList<DiscussionThread>> GetByCourseOfferingAsync(Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(t => t.CourseOfferingId == courseOfferingId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(DiscussionThread thread) => context.DiscussionThreads.Add(thread);

    private IQueryable<DiscussionThread> Query() => context.DiscussionThreads.Include(t => t.Posts);
}
