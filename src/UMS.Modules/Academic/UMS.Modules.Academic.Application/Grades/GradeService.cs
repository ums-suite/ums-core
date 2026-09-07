using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Modules.Academic.Domain.ResultPublications;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.Grades;

/// <summary>
/// ACD-10: Faculty enters/submits marks per Assessment; the aggregate Grade is calculated per the
/// CourseOffering's configured weighting (requirement-spec.md §2 Grade Entry).
///
/// <para>
/// design-decisions.md "Grade-Lock State Machine Design" / edge-cases.md "Grade lock racing a
/// still-in-flight grade submission from Faculty's UI": this submission is only accepted while the
/// parent CourseOffering's `ResultPublication` is `Draft`/`Calculated` - enforced via
/// <see cref="IResultPublicationRepository.TryTransitionAsync"/>'s state-guarded conditional
/// `UPDATE ... WHERE status IN (Draft, Calculated)`, in the SAME transaction as the Grade write.
/// If a Department Head's Lock has already moved the batch to `Verified` (or further) by the time
/// this statement runs, zero rows are affected and the whole transaction is rolled back - the
/// Faculty submission is rejected with an explicit `grade.already_locked` error, never silently
/// discarded and never silently overwritten.
/// </para>
/// </summary>
public sealed class GradeService(
    IEnrollmentRepository enrollments,
    ICourseOfferingRepository offerings,
    IResultPublicationRepository resultPublications,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<GradeDto>> SubmitAsync(Guid submittedByUserId, SubmitGradeRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var enrollmentId = request.EnrollmentId;
        var enrollment = await enrollments.GetByIdAsync(new EnrollmentId(enrollmentId), cancellationToken).ConfigureAwait(false);
        if (enrollment is null)
        {
            return Error.NotFound("grade.enrollment_not_found", $"No Enrollment exists with id '{enrollmentId}'.");
        }

        var offering = await offerings.GetByIdAsync(new CourseOfferingId(enrollment.CourseOfferingId), cancellationToken).ConfigureAwait(false);
        if (offering is null)
        {
            return Error.Failure("grade.courseoffering_missing", "The Enrollment's CourseOffering could not be resolved.");
        }

        var calcResult = GradeCalculator.Calculate(offering, request.Scores.Select(s => (s.AssessmentId, s.Score)).ToList());
        if (calcResult.IsFailure)
        {
            return calcResult.Error!;
        }

        var now = clock.UtcNow;
        var before = enrollment.Grade is { } existingGrade ? JsonSerializer.Serialize(new { existingGrade.CalculatedScore, existingGrade.LetterGrade }) : null;

        enrollment.SubmitGrade(request.Scores.Select(s => (s.AssessmentId, s.Score)).ToList(), calcResult.Value.CalculatedScore, calcResult.Value.LetterGrade, submittedByUserId, now);
        var grade = enrollment.Grade!;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var resultPublication = await resultPublications.GetByCourseOfferingIdAsync(offering.Id.Value, cancellationToken).ConfigureAwait(false);
        if (resultPublication is null)
        {
            resultPublication = ResultPublication.Create(offering.Id.Value, now);
            resultPublications.Add(resultPublication);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DuplicateValueException)
            {
                // A concurrent first-submission for this same CourseOffering already created the
                // row - re-read it and proceed with the guarded transition below exactly as the
                // "already existed" branch would have.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return await SubmitAsync(submittedByUserId, request, audit, cancellationToken).ConfigureAwait(false);
            }
        }

        var transitioned = await resultPublications.TryTransitionAsync(
            resultPublication.Id,
            [ResultPublicationStatus.Draft, ResultPublicationStatus.Calculated],
            ResultPublicationStatus.Calculated,
            columns => columns.CalculatedAt = now,
            cancellationToken).ConfigureAwait(false);

        if (!transitioned)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var current = await resultPublications.GetByCourseOfferingIdAsync(offering.Id.Value, cancellationToken).ConfigureAwait(false);
            return Error.Conflict("grade.already_locked", $"CourseOffering '{offering.Id}''s grade batch is already '{current?.Status}' - Faculty submission is no longer accepted.");
        }

        var auditRequest = audit.ToRequest(
            "Grade",
            grade.Id.Value.ToString(),
            "submit",
            before,
            JsonSerializer.Serialize(new { grade.CalculatedScore, grade.LetterGrade }),
            organizationScopeId: offering.DepartmentId);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(grade);
    }

    internal static GradeDto ToDto(Domain.Enrollments.Grade grade) =>
        new(
            grade.Id.Value,
            grade.EnrollmentId.Value,
            grade.CalculatedScore?.Value,
            grade.LetterGrade,
            grade.Scores.Select(s => new AssessmentScoreDto(s.AssessmentId, s.Score)).ToList(),
            grade.SubmittedAt);
}
