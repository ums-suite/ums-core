namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>STU-10: Student's own local port onto <c>UMS.Shared.Academic.ITranscriptQuery</c> - mirrors <see cref="IDocumentGenerationPort"/>'s own wrapping pattern exactly.</summary>
public interface IAcademicTranscriptPort
{
    public Task<TranscriptSnapshot> GetTranscriptAsync(Guid studentId, CancellationToken cancellationToken = default);
}

public sealed record TranscriptRowSnapshot(Guid CourseOfferingId, Guid CourseId, Guid SemesterId, decimal CalculatedScore, string LetterGrade, decimal CreditHours, DateTimeOffset PublishedAt);

public sealed record TranscriptSnapshot(Guid StudentId, IReadOnlyList<TranscriptRowSnapshot> Rows, decimal? OverallAverage);
