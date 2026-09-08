using UMS.Modules.Alumni.Domain.Jobs;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IJobApplicationRepository
{
    /// <summary>
    /// ALM-7: edge-cases.md "JobPosting expiry sweep racing a concurrent application submission" -
    /// a guarded INSERT that atomically re-validates <c>status = Published AND expires_at &gt; now</c>
    /// against the CURRENT committed row (never a value read earlier in the request) as part of the
    /// same statement that inserts the JobApplication row. Returns <see langword="false"/> (zero rows
    /// affected) when the posting no longer accepts applications - the caller then returns a
    /// conflict, never a JobApplication is left half-created.
    /// </summary>
    public Task<bool> TryInsertIfPostingAcceptsApplicationsAsync(JobApplication application, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<JobApplication>> ListByPostingAsync(JobPostingId jobPostingId, CancellationToken cancellationToken = default);
}
