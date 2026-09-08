using UMS.Modules.Alumni.Domain.Jobs;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IJobPostingRepository
{
    public Task<JobPosting?> GetByIdAsync(JobPostingId id, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §2.3/§6: list results exclude Expired/Removed by default (<paramref name="status"/> lets an Admin/poster explicitly ask for their own non-Published items too).</summary>
    public Task<IReadOnlyList<JobPosting>> ListAsync(JobPostingStatus? status, Guid? posterUserId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>ALM-6: every Published posting whose expires_at has already passed - the sweep's own work queue.</summary>
    public Task<IReadOnlyList<JobPosting>> ListDueForExpiryAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);

    public void Add(JobPosting posting);
}
