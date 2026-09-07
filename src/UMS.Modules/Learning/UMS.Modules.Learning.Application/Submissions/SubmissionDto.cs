namespace UMS.Modules.Learning.Application.Submissions;

public sealed record SubmissionDto(
    Guid Id,
    Guid AssignmentId,
    Guid CourseOfferingId,
    Guid StudentId,
    string? TextContent,
    IReadOnlyCollection<SubmissionFileDto> Files,
    DateTimeOffset SubmittedAt,
    bool IsLate,
    decimal LatePenaltyPercentage,
    string Status,
    Guid? SupersededBySubmissionId,
    DateTimeOffset? SupersededAt,
    AssignmentScoreDto? Score,
    Guid? EvaluatedByUserId,
    DateTimeOffset? EvaluatedAt,
    PlagiarismCheckDto? PlagiarismCheck);

public sealed record SubmissionFileDto(Guid ArtifactId, string FileName, string MimeType);

public sealed record AssignmentScoreDto(decimal RawPoints, decimal AwardedPoints, int MaxPoints, decimal AppliedLatePenaltyPercentage, string Feedback);

/// <summary>
/// LRN-8: surfaced on every Submission read so an Instructor can always tell "checked, low
/// similarity" apart from "never successfully checked" - <c>Status</c> is
/// <c>Queued</c>/<c>Running</c>/<c>Completed</c>/<c>Failed</c>/<c>Cancelled</c> and a
/// <see langword="null"/> <paramref name="SimilarityPercentage"/> on a <c>Failed</c> check is NOT a
/// zero (requirement-spec.md §2/§4).
/// </summary>
public sealed record PlagiarismCheckDto(
    Guid Id,
    string Status,
    decimal? SimilarityPercentage,
    string? MatchedSourceSummary,
    string? ProviderName,
    int AttemptCount,
    DateTimeOffset QueuedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason);

/// <summary><paramref name="Files"/> reference Documents' own <c>UploadedArtifact</c> ids, already requested and confirmed via <c>POST /assignments/{id}/submissions/uploads</c> before this call.</summary>
public sealed record CreateSubmissionRequest(string? TextContent, IReadOnlyCollection<SubmissionFileDto>? Files);

public sealed record EvaluateSubmissionRequest(decimal Points, string? Feedback);

public sealed record RequestSubmissionUploadRequest(string FileName, string MimeType);

public sealed record SubmissionUploadSlotDto(Guid ArtifactId, string Status, string? UploadUrl, string FileName, string MimeType);

public sealed record ConfirmSubmissionUploadRequest(Guid ArtifactId);
