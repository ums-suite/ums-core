namespace UMS.Modules.Alumni.Domain.Jobs;

/// <summary>
/// ALM-7: an applicant's interest record against a JobPosting (requirement-spec.md §2.3 last-but-one
/// bullet, §3 - "Module-Local Term, pending glossary merge"). Not an <c>AggregateRoot</c> - it raises
/// no domain events and its own write-time invariant ("the posting must still accept applications")
/// is enforced by a guarded INSERT against <see cref="JobPosting"/>'s own current state in the SAME
/// transaction (edge-cases.md "JobPosting expiry sweep racing a concurrent application submission"),
/// not by an in-process aggregate check that could read a stale snapshot.
/// </summary>
public sealed class JobApplication
{
    private JobApplication()
    {
    }

    private JobApplication(JobApplicationId id, JobPostingId jobPostingId, Guid applicantUserId, bool applicantIsAlumnus, string? note, Guid? resumeArtifactId, DateTimeOffset submittedAt)
    {
        Id = id;
        JobPostingId = jobPostingId;
        ApplicantUserId = applicantUserId;
        ApplicantIsAlumnus = applicantIsAlumnus;
        Note = note;
        ResumeArtifactId = resumeArtifactId;
        SubmittedAt = submittedAt;
    }

    public JobApplicationId Id { get; private set; }

    public JobPostingId JobPostingId { get; private set; }

    public Guid ApplicantUserId { get; private set; }

    public bool ApplicantIsAlumnus { get; private set; }

    public string? Note { get; private set; }

    /// <summary>requirement-spec.md §7 Documents dependency: optional resume reference via <c>UMS.Shared.Documents.IUploadedArtifactRequester</c> - stores only the artifactId, never file bytes.</summary>
    public Guid? ResumeArtifactId { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public static JobApplication Create(JobPostingId jobPostingId, Guid applicantUserId, bool applicantIsAlumnus, string? note, Guid? resumeArtifactId, DateTimeOffset submittedAt) =>
        new(JobApplicationId.New(), jobPostingId, applicantUserId, applicantIsAlumnus, note, resumeArtifactId, submittedAt);
}
