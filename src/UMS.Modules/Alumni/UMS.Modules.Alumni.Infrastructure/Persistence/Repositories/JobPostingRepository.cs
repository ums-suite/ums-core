using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Jobs;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

internal sealed class JobPostingRepository(AlumniDbContext context) : IJobPostingRepository
{
    public Task<JobPosting?> GetByIdAsync(JobPostingId id, CancellationToken cancellationToken = default) =>
        context.JobPostings.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<JobPosting>> ListAsync(JobPostingStatus? status, Guid? posterUserId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.JobPostings.AsQueryable();

        if (status is { } s)
        {
            query = query.Where(p => p.Status == s);
        }

        if (posterUserId is { } userId)
        {
            query = query.Where(p => p.PosterUserId == userId);
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JobPosting>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.JobPostings
            .Where(p => p.Status == JobPostingStatus.Published && p.ExpiresAt <= now)
            .OrderBy(p => p.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(JobPosting posting) => context.JobPostings.Add(posting);
}
