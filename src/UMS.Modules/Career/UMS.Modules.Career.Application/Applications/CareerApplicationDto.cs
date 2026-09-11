namespace UMS.Modules.Career.Application.Applications;

public sealed record CareerApplicationDto(
    Guid Id,
    Guid StudentId,
    Guid? InternshipId,
    Guid? DriveId,
    string Status,
    decimal? DeclaredCgpa,
    int? DeclaredYearOfStudy,
    Guid ResumeProfileIdSnapshot,
    Guid ResumeArtifactIdSnapshot,
    string ResumeFileNameSnapshot,
    Guid? InterviewSlotId,
    DateTimeOffset SubmittedAt,
    string? DecisionReason,
    DateTimeOffset? DecidedAt,
    uint Version);

/// <param name="ResumeProfileId">Which of the Student's own `ResumeProfile`s to snapshot at submission time (requirement-spec.md §4 snapshot invariant).</param>
public sealed record ApplyToInternshipRequest(Guid ResumeProfileId, decimal? DeclaredCgpa, int? DeclaredYearOfStudy);

public sealed record RegisterForDriveRequest(Guid ResumeProfileId, decimal? DeclaredCgpa, int? DeclaredYearOfStudy);

public sealed record ChangeCareerApplicationStatusRequest(string NewStatus, string? Reason, uint Version);

public sealed record ShortlistDriveApplicationsRequest(IReadOnlyCollection<Guid> CareerApplicationIds);
