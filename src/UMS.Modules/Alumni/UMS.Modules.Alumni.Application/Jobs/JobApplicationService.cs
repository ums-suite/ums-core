using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Jobs;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.Jobs;

/// <summary>
/// ALM-7: <c>POST /jobs/{id}/apply</c> (requirement-spec.md §2.3, §3, §7 Documents). edge-cases.md
/// "JobPosting expiry sweep racing a concurrent application submission": the write-time guard lives
/// in <see cref="IJobApplicationRepository.TryInsertIfPostingAcceptsApplicationsAsync"/>'s own guarded
/// INSERT, not a check performed here before the insert - this service never reads the JobPosting's
/// status itself to decide whether to allow the application.
/// </summary>
public sealed class JobApplicationService(IJobApplicationRepository applications, IClock clock)
{
    public static JobApplicationDto ToDto(JobApplication application) => new(
        application.Id.Value,
        application.JobPostingId.Value,
        application.ApplicantUserId,
        application.ApplicantIsAlumnus,
        application.Note,
        application.ResumeArtifactId,
        application.SubmittedAt);

    public async Task<Result<JobApplicationDto>> ApplyAsync(Guid jobPostingId, Guid applicantUserId, bool applicantIsAlumnus, ApplyToJobRequest request, CancellationToken cancellationToken = default)
    {
        var application = JobApplication.Create(new JobPostingId(jobPostingId), applicantUserId, applicantIsAlumnus, request.Note, request.ResumeArtifactId, clock.UtcNow);

        var accepted = await applications.TryInsertIfPostingAcceptsApplicationsAsync(application, cancellationToken).ConfigureAwait(false);
        if (!accepted)
        {
            return Error.Conflict("jobposting.not_accepting_applications", $"JobPosting '{jobPostingId}' is not currently accepting applications (not Published, or past its expires_at).");
        }

        return ToDto(application);
    }

    public async Task<IReadOnlyList<JobApplicationDto>> ListByPostingAsync(Guid jobPostingId, CancellationToken cancellationToken = default) =>
        (await applications.ListByPostingAsync(new JobPostingId(jobPostingId), cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();
}
