using System.Text.Json;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Application.Common;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.CourseOfferings;

/// <summary>ACD-5: Exam/Assessment configuration for a CourseOffering - gradable components with a configured weight (requirement-spec.md §2 Assessment Configuration).</summary>
public sealed class AssessmentService(ICourseOfferingRepository offerings, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder)
{
    public async Task<Result<CourseOfferingDto>> AddExamAsync(Guid courseOfferingId, CreateExamRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var offering = await offerings.GetByIdAsync(new CourseOfferingId(courseOfferingId), cancellationToken).ConfigureAwait(false);
        if (offering is null)
        {
            return Error.NotFound("courseoffering.not_found", $"No CourseOffering exists with id '{courseOfferingId}'.");
        }

        Exam exam;
        try
        {
            exam = offering.AddExam(request.Name, request.Assessments.Select(a => (a.Name, a.Weight)).ToList());
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Error.Validation("courseoffering.invalid_assessment", ex.Message);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "CourseOffering",
            offering.Id.Value.ToString(),
            "add_exam",
            null,
            JsonSerializer.Serialize(new { examId = exam.Id.Value, exam.Name, assessments = exam.Assessments.Select(a => new { a.Name, a.Weight }) }),
            organizationScopeId: offering.DepartmentId);
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : CourseOfferingService.ToDto(offering);
    }
}
