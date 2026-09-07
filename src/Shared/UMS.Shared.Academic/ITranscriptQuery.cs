namespace UMS.Shared.Academic;

/// <summary>
/// STU-10: the read-only Transcript query Student's own <c>StudentRequestService</c> calls to
/// generate the render data behind a transcript-request `StudentRequest` (student
/// requirement-spec.md §2: "A transcript request calls Academic's transcript query (read model)").
/// Academic's own `StudentResultQueryService.GetTranscriptAsync` already implements this exact read
/// model (Published-grades-only, per Academic's own Transcript-sourcing invariant) - exposed today
/// only via `GET /students/{id}/transcript`; this interface is the in-process contract Student calls
/// instead of an HTTP hop (ADR-0003's command/query semantics: one caller, one authoritative
/// result).
///
/// <para>
/// Living in <c>UMS.Shared.Academic</c> - not <c>UMS.Modules.Academic.*</c> - is what lets Student
/// call it without a forbidden dependency on Academic's Domain/Application/Infrastructure internals
/// (module-boundaries.md, ADR-0002), mirroring <see cref="ICourseOfferingLookup"/>'s exact "first
/// mover, no stub-then-promote dance needed" doc-comment pattern - Student is the first mover for
/// this particular outward-facing contract. Academic's own Infrastructure layer registers the one
/// real implementation, adapting this interface onto <c>StudentResultQueryService</c>.
/// </para>
///
/// <para>
/// <b>Deliberately NOT the reverse.</b> module-boundaries.md's dependency table already states
/// Academic depends on Student (for status/scope), never the other direction for Student's OWN
/// data - this interface only lets Student pull Academic's read model for a Student it already
/// knows the id of, it never lets Academic reach back into Student. No new cycle is introduced.
/// </para>
/// </summary>
public interface ITranscriptQuery
{
    /// <summary>Never throws for a Student with zero Published results - resolves to an empty <see cref="TranscriptSummary.Rows"/> and a <see langword="null"/> <see cref="TranscriptSummary.OverallAverage"/>, exactly as Academic's own read model does for the same case (edge-cases.md academic, "Transcript requested mid-semester"). A <c>Graduated</c> Student is served identically - "Graduated is not a deletion or migration event" (student edge-cases.md).</summary>
    public Task<TranscriptSummary> GetAsync(Guid studentId, CancellationToken cancellationToken = default);
}

/// <summary>One row of a Student's Transcript - a single Published, graded Enrollment.</summary>
public sealed record TranscriptRowSummary(Guid CourseOfferingId, Guid CourseId, Guid SemesterId, decimal CalculatedScore, string LetterGrade, decimal CreditHours, DateTimeOffset PublishedAt);

public sealed record TranscriptSummary(Guid StudentId, IReadOnlyList<TranscriptRowSummary> Rows, decimal? OverallAverage);
