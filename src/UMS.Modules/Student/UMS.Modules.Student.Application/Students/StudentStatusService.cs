using System.Text.Json;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Application.Students;

/// <summary>
/// STU-8: the full status state machine (requirement-spec.md §2/§4/§8; design-decisions.md,
/// "Status-Change Transactional Boundary"). Optimistic concurrency via <c>xmin</c>, exactly
/// Faculty's own pattern - but see edge-cases.md's "Two admins issue conflicting status changes"
/// refinement this class's own <see cref="ChangeStatusAsync"/> doc comment restates: the loser is
/// rejected outright and shown the CURRENT state, never blind-retried - <c>StudentEndpoints</c>'s
/// own status-change route is what actually attaches that current-state payload to the 409
/// response (this application service returns a plain <see cref="Error.Conflict"/>, exactly like
/// every other module's version-conflict handling; the caller re-fetches via
/// <see cref="StudentProfileService.GetByIdAsync"/>, a small, documented deviation for this one
/// endpoint rather than widening the shared <c>Result</c>/<c>Error</c> shape platform-wide for a
/// single call site).
/// </summary>
public sealed class StudentStatusService(
    IStudentRepository students,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>
    /// edge-cases.md "Two admins issue conflicting status changes to the same Student
    /// concurrently": the second writer's own retry must re-validate against the CURRENT status
    /// (this method reads fresh, applies the legal-transition check inside
    /// <see cref="Domain.Students.Student.ChangeStatus"/>, then attempts the version-checked save) -
    /// never blindly re-apply what was legal against stale state. This edge case's own residual
    /// note - "both admins' attempted actions are independently audited... even though only one is
    /// ever applied" - is why a rejected attempt (illegal transition OR a lost version race) still
    /// writes its own <c>status_change_rejected</c> Audit entry via <see cref="RecordRejectedAttemptAsync"/>,
    /// separate from (and never touching) the entity-mutation transaction that failed.
    /// </summary>
    public async Task<Result<StudentDto>> ChangeStatusAsync(Guid id, Guid callerUserId, ChangeStudentStatusRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<StudentStatus>(request.Status, ignoreCase: true, out var newStatus))
        {
            return Error.Validation("student.invalid_status", $"'{request.Status}' is not a valid Student status.");
        }

        var student = await students.GetByIdAsync(new StudentId(id), cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", $"No Student exists with id '{id}'.");
        }

        var previousStatus = student.Status;

        try
        {
            student.ChangeStatus(newStatus, request.Reason, callerUserId, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            await RecordRejectedAttemptAsync(id, previousStatus, newStatus, audit, callerUserId, "illegal_transition", cancellationToken).ConfigureAwait(false);
            return Error.Conflict("student.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(student, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // ums-requirements.md §4.1: "student status change" is explicitly on the sensitive-
        // mutation list - synchronous Audit write, no exceptions (ADR-0012).
        var auditRequest = audit.ToRequest(
            "Student",
            id.ToString(),
            "status_change",
            JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
            JsonSerializer.Serialize(new { status = newStatus.ToString() }),
            request.Reason,
            organizationScopeId: student.DepartmentId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            if (commitResult.Error!.Code == "student.version_conflict")
            {
                // The failed SaveChanges above has already rolled back its own transaction - this
                // opens an independent one, writing ONLY through Audit's own separate DbContext
                // (bound to that new transaction), so the still-stale tracked Student entity from
                // the failed attempt is never touched/saved again here.
                await RecordRejectedAttemptAsync(id, previousStatus, newStatus, audit, callerUserId, "version_conflict", cancellationToken).ConfigureAwait(false);
            }

            return commitResult.Error!;
        }

        return CreateStudentRecordService.ToDto(student);
    }

    private async Task RecordRejectedAttemptAsync(Guid studentId, StudentStatus attemptedFrom, StudentStatus attemptedTo, AuditContext audit, Guid callerUserId, string rejectionReasonCode, CancellationToken cancellationToken)
    {
        try
        {
            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = audit.ToRequest(
                "Student",
                studentId.ToString(),
                "status_change_rejected",
                JsonSerializer.Serialize(new { status = attemptedFrom.ToString() }),
                JsonSerializer.Serialize(new { attemptedStatus = attemptedTo.ToString(), actorUserId = callerUserId }),
                reason: $"Rejected: {rejectionReasonCode}.");

            var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
            if (auditResult.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort - edge-cases.md's residual note is an audit-trail completeness
            // enhancement, never allowed to mask the primary Conflict response already being
            // returned to the caller for the rejected attempt itself.
        }
    }
}
