namespace UMS.Modules.Learning.Application.Assignments;

public sealed record AssignmentDto(
    Guid Id,
    Guid CourseOfferingId,
    string Title,
    string Instructions,
    string AllowedSubmissionType,
    bool AllowResubmission,
    int MaxPoints,
    string Status,
    SubmissionWindowDto SubmissionWindow,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    IReadOnlyCollection<SubmissionExtensionDto> Extensions);

/// <summary><c>EffectiveDeadline</c> is exposed alongside <c>Deadline</c> deliberately: the UI shows the stated <c>Deadline</c> to Students, while an Instructor's own grading view needs to see the padded value acceptance is actually decided against (edge-cases.md's grace-period entry).</summary>
public sealed record SubmissionWindowDto(
    DateTimeOffset OpensAt,
    DateTimeOffset Deadline,
    TimeSpan GracePeriod,
    DateTimeOffset EffectiveDeadline,
    DateTimeOffset HardCloseAt,
    IReadOnlyCollection<LatePenaltyTierDto> LatePenaltyTiers);

public sealed record LatePenaltyTierDto(TimeSpan MaxLateness, decimal DeductionPercentage);

public sealed record SubmissionExtensionDto(
    Guid Id,
    Guid StudentId,
    DateTimeOffset ExtendedDeadline,
    Guid GrantedByUserId,
    string Reason,
    bool WaivesLatePenalty,
    DateTimeOffset GrantedAt);

public sealed record CreateAssignmentRequest(
    Guid CourseOfferingId,
    string Title,
    string? Instructions,
    string AllowedSubmissionType,
    bool AllowResubmission,
    int MaxPoints,
    DateTimeOffset OpensAt,
    DateTimeOffset Deadline,
    TimeSpan? GracePeriod,
    DateTimeOffset HardCloseAt,
    IReadOnlyCollection<LatePenaltyTierDto>? LatePenaltyTiers);

public sealed record CancelAssignmentRequest(string Reason);

/// <summary><paramref name="StudentId"/> is the Student's own id (not their Identity user id) - an Instructor granting an accommodation names the Student, and Learning resolves the backing user through Academic's own enrollment lookup.</summary>
public sealed record GrantSubmissionExtensionRequest(
    Guid StudentId,
    DateTimeOffset ExtendedDeadline,
    string Reason,
    bool WaivesLatePenalty);
