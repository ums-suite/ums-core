namespace UMS.Modules.Academic.Application.Grades;

public sealed record CorrectGradeRequest(IReadOnlyCollection<AssessmentScoreDto> Scores, string Reason);
