using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.ResultPublications;

namespace UMS.Modules.Academic.Application.ResultPublications;

/// <summary>
/// ACD-14/ACD-15: requirement-spec.md §4's Transcript-sourcing invariant, applied uniformly to
/// BOTH a Student's own results query and their Transcript - "reads only Grades attached to a
/// Published ResultPublication ... an in-progress semester never contributes partial rows"
/// (edge-cases.md "Transcript requested mid-semester"). A `Draft`/`Calculated`/`Verified`/
/// `Approved` grade batch's Enrollments are excluded ENTIRELY here, not surfaced as
/// partial/provisional - this is the one read path shared by ACD-14's <c>GET /students/{id}/results</c>
/// and ACD-15's <c>GET /students/{id}/transcript</c>.
/// </summary>
public sealed class StudentResultQueryService(IEnrollmentRepository enrollments, ICourseOfferingRepository offerings, IResultPublicationRepository resultPublications)
{
    public async Task<IReadOnlyList<StudentResultRowDto>> GetPublishedResultsAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var candidates = await enrollments.GetPublishedByStudentAsync(studentId, cancellationToken).ConfigureAwait(false);
        var rows = new List<StudentResultRowDto>();

        foreach (var enrollment in candidates)
        {
            if (enrollment.Grade is not { CalculatedScore: not null, LetterGrade: not null } grade)
            {
                continue;
            }

            var resultPublication = await resultPublications.GetByCourseOfferingIdAsync(enrollment.CourseOfferingId, cancellationToken).ConfigureAwait(false);
            if (resultPublication is not { Status: ResultPublicationStatus.Published, PublishedAt: { } publishedAt })
            {
                // requirement-spec.md §4/§8: excluded entirely, never partial/provisional.
                continue;
            }

            var offering = await offerings.GetByIdAsync(new CourseOfferingId(enrollment.CourseOfferingId), cancellationToken).ConfigureAwait(false);
            if (offering is null)
            {
                continue;
            }

            rows.Add(new StudentResultRowDto(
                enrollment.Id.Value,
                enrollment.CourseOfferingId,
                offering.CourseId,
                enrollment.SemesterId,
                grade.CalculatedScore.Value,
                grade.LetterGrade,
                enrollment.CreditHoursAtEnrollment.Value,
                publishedAt));
        }

        return rows;
    }

    public async Task<TranscriptDto> GetTranscriptAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var rows = await GetPublishedResultsAsync(studentId, cancellationToken).ConfigureAwait(false);
        var overallAverage = rows.Count == 0 ? null : (decimal?)Math.Round(rows.Average(r => r.CalculatedScore), 2);
        return new TranscriptDto(studentId, rows, overallAverage);
    }
}
