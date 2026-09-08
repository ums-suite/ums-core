using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Common;
using UMS.Modules.Career.Domain.Applications;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Career.Application.Applications;

/// <summary>
/// CAR-7/CAR-8/CAR-11: staff status review, Student-initiated withdrawal, and bulk drive
/// shortlisting (requirement-spec.md §2.4, §5, §6). §5 Auditability: `Offered`/`Rejected` final
/// decisions are audited (ADR-0012) - every other transition is not (mirrors Alumni's own
/// moderation-vs-plain-edit audit split).
/// </summary>
public sealed class CareerApplicationReviewService(ICareerApplicationRepository applications, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, IClock clock)
{
    private static readonly HashSet<CareerApplicationStatus> AuditedFinalDecisions = [CareerApplicationStatus.Offered, CareerApplicationStatus.Rejected];

    public async Task<Result<CareerApplicationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new CareerApplicationId(id), cancellationToken).ConfigureAwait(false);
        return application is null ? Error.NotFound("careerapplication.not_found", $"No CareerApplication exists with id '{id}'.") : CareerApplicationMapper.ToDto(application);
    }

    public async Task<IReadOnlyList<CareerApplicationDto>> ListByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        (await applications.ListByStudentAsync(studentId, cancellationToken).ConfigureAwait(false)).Select(CareerApplicationMapper.ToDto).ToList();

    public async Task<IReadOnlyList<CareerApplicationDto>> ListByInternshipAsync(Guid internshipId, CancellationToken cancellationToken = default) =>
        (await applications.ListByInternshipAsync(new Domain.Internships.InternshipId(internshipId), cancellationToken).ConfigureAwait(false)).Select(CareerApplicationMapper.ToDto).ToList();

    public async Task<IReadOnlyList<CareerApplicationDto>> ListByDriveAsync(Guid driveId, CancellationToken cancellationToken = default) =>
        (await applications.ListByDriveAsync(new Domain.Drives.CampusRecruitmentDriveId(driveId), cancellationToken).ConfigureAwait(false)).Select(CareerApplicationMapper.ToDto).ToList();

    /// <summary>CAR-7: `POST /applications/{id}/status` - reason required, audited when the resulting status is a final decision (`Offered`/`Rejected`).</summary>
    public async Task<Result<CareerApplicationDto>> ChangeStatusAsync(Guid id, ChangeCareerApplicationStatusRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new CareerApplicationId(id), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return Error.NotFound("careerapplication.not_found", $"No CareerApplication exists with id '{id}'.");
        }

        if (!Enum.TryParse<CareerApplicationStatus>(request.NewStatus, ignoreCase: true, out var newStatus))
        {
            return Error.Validation("careerapplication.invalid_status", $"'{request.NewStatus}' is not a valid CareerApplication status.");
        }

        var previousStatus = application.Status;

        try
        {
            application.TransitionTo(newStatus, request.Reason, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("careerapplication.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(application, request.Version);

        if (!AuditedFinalDecisions.Contains(newStatus))
        {
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ConcurrencyConflictException ex)
            {
                return Error.Conflict("careerapplication.concurrency_conflict", ex.Message);
            }

            return CareerApplicationMapper.ToDto(application);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "CareerApplication",
            application.Id.Value.ToString(),
            newStatus == CareerApplicationStatus.Offered ? AuditActions.Approve : AuditActions.Reject,
            $$"""{"status":"{{previousStatus}}"}""",
            $$"""{"status":"{{newStatus}}"}""",
            reason: request.Reason);

        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : CareerApplicationMapper.ToDto(application);
    }

    /// <summary>CAR-8: Student-initiated withdrawal - available before any terminal status, no cross-application side effects (requirement-spec.md §4).</summary>
    public async Task<Result<CareerApplicationDto>> WithdrawAsync(Guid id, Guid studentId, uint version, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new CareerApplicationId(id), cancellationToken).ConfigureAwait(false);
        if (application is null || application.StudentId != studentId)
        {
            return Error.NotFound("careerapplication.not_found", $"No CareerApplication exists with id '{id}' for this Student.");
        }

        try
        {
            application.Withdraw(clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("careerapplication.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(application, version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("careerapplication.concurrency_conflict", ex.Message);
        }

        return CareerApplicationMapper.ToDto(application);
    }

    /// <summary>CAR-11: `POST /drives/{id}/shortlist` (staff, `career.drive.manage`) - bulk-transitions the named `CareerApplication`s (Submitted/UnderReview) to `Shortlisted`, skipping any already-terminal or already-Shortlisted entries rather than failing the whole batch.</summary>
    public async Task<IReadOnlyList<CareerApplicationDto>> ShortlistAsync(IReadOnlyCollection<Guid> careerApplicationIds, CancellationToken cancellationToken = default)
    {
        var results = new List<CareerApplicationDto>();
        foreach (var applicationId in careerApplicationIds)
        {
            var application = await applications.GetByIdAsync(new CareerApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
            if (application is null || application.Status is not (CareerApplicationStatus.Submitted or CareerApplicationStatus.UnderReview))
            {
                continue;
            }

            application.TransitionTo(CareerApplicationStatus.Shortlisted, reason: null, clock.UtcNow);
            results.Add(CareerApplicationMapper.ToDto(application));
        }

        if (results.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return results;
    }
}
