namespace UMS.Modules.Alumni.Application.Jobs;

public sealed record JobApplicationDto(Guid Id, Guid JobPostingId, Guid ApplicantUserId, bool ApplicantIsAlumnus, string? Note, Guid? ResumeArtifactId, DateTimeOffset SubmittedAt);

public sealed record ApplyToJobRequest(string? Note, Guid? ResumeArtifactId);
