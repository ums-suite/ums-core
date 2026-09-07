namespace UMS.Modules.Academic.Application.Grades;

public sealed record GradeDto(Guid Id, Guid EnrollmentId, decimal? CalculatedScore, string? LetterGrade, IReadOnlyCollection<AssessmentScoreDto> Scores, DateTimeOffset? SubmittedAt);

public sealed record AssessmentScoreDto(Guid AssessmentId, decimal Score);

public sealed record SubmitGradeRequest(Guid EnrollmentId, IReadOnlyCollection<AssessmentScoreDto> Scores);
