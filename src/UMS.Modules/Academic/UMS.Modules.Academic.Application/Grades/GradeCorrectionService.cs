using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Modules.Academic.Domain.Events;
using UMS.Modules.Academic.Domain.ResultPublications;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.Grades;

/// <summary>
/// ACD-13: Grade correction after lock/publish - a controlled, audited workflow (Department-Head-
/// reviewed, Registrar-approved upstream of this call; this service performs the actual value
/// mutation once authorized). requirement-spec.md §2/§9 decision 3: reuses `ResultPublication`'s
/// own state machine, re-entering at `Verified` - NEVER a raw, direct `UPDATE` of a Published Grade
/// (requirement-spec.md §4 grade-lock-immutability invariant). The re-entry is the identical
/// state-guarded conditional update (`WHERE status = Published`) every other transition in this
/// module uses.
/// </summary>
public sealed class GradeCorrectionService(
    IEnrollmentRepository enrollments,
    ICourseOfferingRepository offerings,
    IResultPublicationRepository resultPublications,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder eventRecorder,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<GradeDto>> CorrectAsync(Guid gradeId, Guid correctedByUserId, CorrectGradeRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var enrollment = await enrollments.GetByGradeIdAsync(new GradeId(gradeId), cancellationToken).ConfigureAwait(false);
        if (enrollment?.Grade is null)
        {
            return Error.NotFound("grade.not_found", $"No Grade exists with id '{gradeId}'.");
        }

        var resultPublication = await resultPublications.GetByCourseOfferingIdAsync(enrollment.CourseOfferingId, cancellationToken).ConfigureAwait(false);
        if (resultPublication is null)
        {
            return Error.Conflict("grade.no_result_publication", "This Enrollment's CourseOffering has no ResultPublication yet - nothing to correct.");
        }

        if (resultPublication.Status != ResultPublicationStatus.Published)
        {
            return Error.Conflict("grade.correction_requires_published", $"CourseOffering's grade batch is '{resultPublication.Status}', not 'Published' - the correction workflow only applies to an already-Published result.");
        }

        var offering = await offerings.GetByIdAsync(new CourseOfferingId(enrollment.CourseOfferingId), cancellationToken).ConfigureAwait(false);
        if (offering is null)
        {
            return Error.Failure("grade.courseoffering_missing", "The Enrollment's CourseOffering could not be resolved.");
        }

        // Reuse the identical calculation the initial submission used - a correction still
        // computes off the CourseOffering's own configured Assessment weighting, never a
        // free-form override value.
        var calcInputs = request.Scores.Select(s => (s.AssessmentId, s.Score)).ToList();
        var calcResult = GradeCalculator.Calculate(offering, calcInputs);
        if (calcResult.IsFailure)
        {
            return calcResult.Error!;
        }

        var now = clock.UtcNow;
        var previousScore = enrollment.Grade.CalculatedScore?.Value ?? 0;
        var before = JsonSerializer.Serialize(new { enrollment.Grade.CalculatedScore, enrollment.Grade.LetterGrade });

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var reentered = await resultPublications.TryTransitionAsync(
            resultPublication.Id,
            [ResultPublicationStatus.Published],
            ResultPublicationStatus.Verified,
            columns =>
            {
                columns.IncrementCorrectionCount = true;
                columns.ClearPublishedMetadata = true;
            },
            cancellationToken).ConfigureAwait(false);

        if (!reentered)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var actual = await resultPublications.GetByCourseOfferingIdAsync(enrollment.CourseOfferingId, cancellationToken).ConfigureAwait(false);
            return Error.Conflict("grade.already_reentered", $"CourseOffering's grade batch is already '{actual?.Status}' - another correction may already be in progress.");
        }

        enrollment.Grade.Correct(calcResult.Value.CalculatedScore, calcResult.Value.LetterGrade, request.Reason, correctedByUserId, now);
        eventRecorder.Enqueue(new GradeCorrected(gradeId, enrollment.Id.Value, resultPublication.Id.Value, previousScore, calcResult.Value.CalculatedScore.Value, request.Reason, correctedByUserId, now));

        var auditRequest = audit.ToRequest("Grade", gradeId.ToString(), "correct", before, JsonSerializer.Serialize(new { calcResult.Value.CalculatedScore, calcResult.Value.LetterGrade }), reason: request.Reason);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : GradeService.ToDto(enrollment.Grade);
    }
}
