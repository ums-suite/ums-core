using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Jobs;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

/// <summary>
/// ALM-7: edge-cases.md "JobPosting expiry sweep racing a concurrent application submission" - see
/// <see cref="IJobApplicationRepository.TryInsertIfPostingAcceptsApplicationsAsync"/>'s own remarks.
/// A guarded <c>INSERT ... SELECT ... WHERE</c> is single, atomic Postgres statement: the WHERE
/// clause is evaluated against the row as it stands at the instant of the write itself, not a value
/// read earlier in this (or any other) transaction - the exact same "recheck inside the write, not
/// before it" discipline the mentor-capacity conditional UPDATE uses, applied here as a conditional
/// INSERT instead. Uses Postgres' own <c>now()</c> (not a C#-side clock value) so the boundary is
/// exactly the database's own commit-time clock, never subject to any app-server clock skew.
/// </summary>
internal sealed class JobApplicationRepository(AlumniDbContext context) : IJobApplicationRepository
{
    public async Task<bool> TryInsertIfPostingAcceptsApplicationsAsync(JobApplication application, CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO alumni.job_applications (id, job_posting_id, applicant_user_id, applicant_is_alumnus, note, resume_artifact_id, submitted_at)
             SELECT {application.Id.Value}, {application.JobPostingId.Value}, {application.ApplicantUserId}, {application.ApplicantIsAlumnus}, {application.Note}, {application.ResumeArtifactId}, {application.SubmittedAt}
             WHERE EXISTS (
                 SELECT 1 FROM alumni.job_postings
                 WHERE id = {application.JobPostingId.Value} AND status = 'Published' AND expires_at > now()
             )
             """,
            cancellationToken).ConfigureAwait(false);

        return affected == 1;
    }

    public async Task<IReadOnlyList<JobApplication>> ListByPostingAsync(JobPostingId jobPostingId, CancellationToken cancellationToken = default) =>
        await context.JobApplications
            .Where(a => a.JobPostingId == jobPostingId)
            .OrderByDescending(a => a.SubmittedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
