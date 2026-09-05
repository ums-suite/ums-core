namespace UMS.Modules.Academic.Application.ResultPublications;

/// <summary>ACD-14/ACD-15: one Published course result row - the shape both the student-results query and the Transcript read model share.</summary>
public sealed record StudentResultRowDto(Guid EnrollmentId, Guid CourseOfferingId, Guid CourseId, Guid SemesterId, decimal CalculatedScore, string LetterGrade, int CreditHours, DateTimeOffset PublishedAt);

public sealed record TranscriptDto(Guid StudentId, IReadOnlyCollection<StudentResultRowDto> Results, decimal? OverallAverageScore);
