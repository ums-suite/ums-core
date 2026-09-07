using UMS.Modules.Academic.Application.ResultPublications;
using UMS.Shared.Academic;

namespace UMS.Modules.Academic.Infrastructure.CrossModule;

/// <summary>The one real implementation of <see cref="ITranscriptQuery"/> - delegates straight onto the already-built <see cref="StudentResultQueryService.GetTranscriptAsync"/> read model (STU-10). See that interface's own remarks for why this lives in <c>UMS.Shared.Academic</c> rather than being called over HTTP.</summary>
internal sealed class TranscriptQueryAdapter(StudentResultQueryService studentResultQueryService) : ITranscriptQuery
{
    public async Task<TranscriptSummary> GetAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var transcript = await studentResultQueryService.GetTranscriptAsync(studentId, cancellationToken).ConfigureAwait(false);

        var rows = transcript.Results
            .Select(r => new TranscriptRowSummary(r.CourseOfferingId, r.CourseId, r.SemesterId, r.CalculatedScore, r.LetterGrade, r.CreditHours, r.PublishedAt))
            .ToList();

        return new TranscriptSummary(transcript.StudentId, rows, transcript.OverallAverageScore);
    }
}
