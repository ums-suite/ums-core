using UMS.Modules.Student.Application.Abstractions;
using UMS.Shared.Academic;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>STU-10: adapts <c>UMS.Shared.Academic.ITranscriptQuery</c> to Student's own local port.</summary>
internal sealed class AcademicTranscriptPortAdapter(ITranscriptQuery transcriptQuery) : IAcademicTranscriptPort
{
    public async Task<TranscriptSnapshot> GetTranscriptAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var transcript = await transcriptQuery.GetAsync(studentId, cancellationToken).ConfigureAwait(false);
        var rows = transcript.Rows
            .Select(r => new TranscriptRowSnapshot(r.CourseOfferingId, r.CourseId, r.SemesterId, r.CalculatedScore, r.LetterGrade, r.CreditHours, r.PublishedAt))
            .ToList();

        return new TranscriptSnapshot(transcript.StudentId, rows, transcript.OverallAverage);
    }
}
