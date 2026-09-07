using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Permissions;
using UMS.Modules.Student.Domain.StudentRequests;
using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Application.StudentRequests;

/// <summary>
/// STU-9..STU-14: the `StudentRequest` submit/read/decide workflow (requirement-spec.md student §2
/// Student-Initiated Requests, §4, §6). One service for all three request types (ID reissue,
/// transcript, grievance) - see <c>StudentRequest</c>'s own remarks for why they share one
/// aggregate/one service rather than three parallel ones.
///
/// <para>
/// <b>Authorization shape, mirroring <c>StudentResultEndpoints</c>/Faculty's <c>LeaveRequestService</c>:</b>
/// the owning Student may always read their own request; a reviewer additionally needs the bare
/// <see cref="StudentPermissions.RequestReview"/> permission (checked by the endpoint's
/// <c>RequirePermission</c>/ad-hoc <c>IPermissionResolver</c> check, never here) AND, for a Grievance
/// routed to a specific Department/Faculty scope, an ADR-0006 ScopeGrant covering that exact scope
/// (<see cref="IReviewerScopeDirectory"/>, checked here) - "a Department Head must see only
/// grievances scoped to their own department, never another's" (requirement-spec.md §5).
/// </para>
/// </summary>
public sealed class StudentRequestService(
    IStudentRequestRepository studentRequests,
    IStudentRepository students,
    IDepartmentFacultyLookup departmentFacultyLookup,
    IReviewerScopeDirectory reviewerScopeDirectory,
    IAcademicTranscriptPort transcriptPort,
    IDocumentGenerationPort documentGeneration,
    INotificationRequestPublisher notifications,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock,
    ILogger<StudentRequestService> logger)
{
    public static StudentRequestDto ToDto(StudentRequest request) => new(
        request.Id.Value,
        request.StudentId,
        request.RequestType.ToString(),
        request.Details,
        request.Status.ToString(),
        request.ReviewScopeNodeId,
        request.IsAgainstOwnDepartmentHead,
        request.GeneratedDocumentId,
        request.DecidedByUserId,
        request.DecisionReason,
        request.SubmittedAt,
        request.DecidedAt,
        request.FulfilledAt,
        request.Version);

    /// <summary>STU-9: rejected for a Suspended Student (edge-cases.md, §9 decision 6).</summary>
    public async Task<Result<StudentRequestDto>> SubmitIdReissueAsync(Guid callerUserId, SubmitIdReissueRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to the calling User.");
        }

        return await SubmitAsync(student, StudentRequestType.IdReissue, () => StudentRequest.SubmitIdReissue(student.Id.Value, request.Reason, clock.UtcNow), audit, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>STU-10: same Suspended-Student restriction as <see cref="SubmitIdReissueAsync"/>.</summary>
    public async Task<Result<StudentRequestDto>> SubmitTranscriptRequestAsync(Guid callerUserId, SubmitTranscriptRequestRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to the calling User.");
        }

        return await SubmitAsync(student, StudentRequestType.TranscriptRequest, () => StudentRequest.SubmitTranscriptRequest(student.Id.Value, request.Purpose, clock.UtcNow), audit, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>STU-11: the one request type a Suspended Student may still file. Resolves the routing scope BEFORE constructing the aggregate (edge-cases.md's own-Department-Head escalation).</summary>
    public async Task<Result<StudentRequestDto>> SubmitGrievanceAsync(Guid callerUserId, SubmitGrievanceRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var student = await students.GetByUserIdAsync(callerUserId, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student.not_found", "No Student profile is linked to the calling User.");
        }

        Guid? reviewScopeNodeId;
        if (request.IsAgainstOwnDepartmentHead)
        {
            // edge-cases.md "A grievance is filed against a Department Head's own department":
            // escalate one scope level up. A null resolution (no parent Faculty found) falls back
            // to the bare, university-wide studentrequest.review permission - see
            // StudentRequest.ReviewScopeNodeId's own remarks - rather than blocking submission.
            reviewScopeNodeId = await departmentFacultyLookup.GetParentFacultyIdAsync(student.DepartmentId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            reviewScopeNodeId = student.DepartmentId;
        }

        return await SubmitAsync(student, StudentRequestType.Grievance, () => StudentRequest.SubmitGrievance(student.Id.Value, request.Description, reviewScopeNodeId, request.IsAgainstOwnDepartmentHead, clock.UtcNow), audit, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>STU-12: owner or an authorized reviewer (see this class's own remarks).</summary>
    public async Task<Result<StudentRequestDto>> GetByIdAsync(Guid id, Guid callerUserId, bool callerHasReviewPermission, CancellationToken cancellationToken = default)
    {
        var request = await studentRequests.GetByIdAsync(new StudentRequestId(id), cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return Error.NotFound("studentrequest.not_found", $"No StudentRequest exists with id '{id}'.");
        }

        if (!await IsOwnerOrAuthorizedReviewerAsync(request, callerUserId, callerHasReviewPermission, cancellationToken).ConfigureAwait(false))
        {
            return Error.Forbidden("studentrequest.access_forbidden", "Only the owning Student or an authorized reviewer may view this StudentRequest.");
        }

        return ToDto(request);
    }

    /// <summary>STU-13: Department Head/Registrar. Triggers fulfillment immediately after a successful Approve (this class's own remarks; <c>StudentRequest.Fulfill</c>'s own remarks on the best-effort posture for document-backed types).</summary>
    public async Task<Result<StudentRequestDto>> ApproveAsync(Guid id, Guid approverUserId, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var request = await studentRequests.GetByIdAsync(new StudentRequestId(id), cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return Error.NotFound("studentrequest.not_found", $"No StudentRequest exists with id '{id}'.");
        }

        if (!await IsAuthorizedReviewerAsync(request, approverUserId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Forbidden("studentrequest.review_forbidden", "The calling reviewer's ScopeGrant does not cover this StudentRequest's routed scope.");
        }

        var beforeStatus = request.Status.ToString();
        try
        {
            request.Approve(approverUserId, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("studentrequest.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(request, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "StudentRequest",
            id.ToString(),
            AuditActions.Approve,
            JsonSerializer.Serialize(new { status = beforeStatus }),
            JsonSerializer.Serialize(new { status = request.Status.ToString() }),
            organizationScopeId: request.ReviewScopeNodeId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            return commitResult.Error!;
        }

        await TryFulfillAsync(request, audit.CorrelationId, cancellationToken).ConfigureAwait(false);
        return ToDto(request);
    }

    /// <summary>STU-14: reason required.</summary>
    public async Task<Result<StudentRequestDto>> RejectAsync(Guid id, Guid approverUserId, RejectStudentRequestRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var studentRequest = await studentRequests.GetByIdAsync(new StudentRequestId(id), cancellationToken).ConfigureAwait(false);
        if (studentRequest is null)
        {
            return Error.NotFound("studentrequest.not_found", $"No StudentRequest exists with id '{id}'.");
        }

        if (!await IsAuthorizedReviewerAsync(studentRequest, approverUserId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Forbidden("studentrequest.review_forbidden", "The calling reviewer's ScopeGrant does not cover this StudentRequest's routed scope.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("studentrequest.reject_reason_required", "A reason is required to reject a StudentRequest.");
        }

        var beforeStatus = studentRequest.Status.ToString();
        try
        {
            studentRequest.Reject(approverUserId, request.Reason, clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("studentrequest.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(studentRequest, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "StudentRequest",
            id.ToString(),
            AuditActions.Reject,
            JsonSerializer.Serialize(new { status = beforeStatus }),
            JsonSerializer.Serialize(new { status = studentRequest.Status.ToString() }),
            request.Reason,
            organizationScopeId: studentRequest.ReviewScopeNodeId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(studentRequest);
    }

    private static Error DuplicateOpenRequestError(StudentRequest existing) =>
        Error.Conflict("studentrequest.duplicate_open_request", $"An open {existing.RequestType} request already exists (#{existing.Id.Value}) - only one open request per type is allowed at a time.");

    private async Task<Result<StudentRequestDto>> SubmitAsync(Domain.Students.Student student, StudentRequestType requestType, Func<StudentRequest> factory, AuditContext audit, CancellationToken cancellationToken)
    {
        // edge-cases.md / §9 decision 6: a Suspended Student retains ONLY grievance/appeal-type
        // request access.
        if (requestType != StudentRequestType.Grievance && student.Status == StudentStatus.Suspended)
        {
            return Error.Forbidden("studentrequest.suspended_restricted", "A Suspended Student may only file grievance/appeal-type requests.");
        }

        // design-decisions.md "StudentRequest Dedup Mechanism": fast-path pre-check only - the
        // partial unique index is the real enforcement, caught below.
        var existingOpen = await studentRequests.GetOpenByStudentAndTypeAsync(student.Id.Value, requestType, cancellationToken).ConfigureAwait(false);
        if (existingOpen is not null)
        {
            return DuplicateOpenRequestError(existingOpen);
        }

        StudentRequest studentRequest;
        try
        {
            studentRequest = factory();
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("studentrequest.invalid", ex.Message);
        }

        studentRequests.Add(studentRequest);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "StudentRequest",
            studentRequest.Id.Value.ToString(),
            AuditActions.Create,
            null,
            JsonSerializer.Serialize(new { requestType = requestType.ToString() }),
            organizationScopeId: studentRequest.ReviewScopeNodeId ?? student.DepartmentId);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
        {
            if (commitResult.Error!.Code == "studentrequest.duplicate_value")
            {
                // edge-cases.md's genuine concurrent-race loser - the partial unique index just
                // caught what the fast-path pre-check above could not see in time.
                var winner = await studentRequests.GetOpenByStudentAndTypeAsync(student.Id.Value, requestType, cancellationToken).ConfigureAwait(false);
                if (winner is not null)
                {
                    return DuplicateOpenRequestError(winner);
                }
            }

            return commitResult.Error!;
        }

        await NotifyReviewersBestEffortAsync(studentRequest, audit.CorrelationId, cancellationToken).ConfigureAwait(false);
        return ToDto(studentRequest);
    }

    /// <summary>
    /// STU-13's own "triggers fulfillment" step. Grievance fulfillment IS the decision itself (no
    /// downstream integration); ID reissue/transcript each call out to Documents (and, for
    /// transcript, Academic first) - a failure here leaves the request sitting in `Approved`
    /// (fulfillment-pending, operator-retryable) rather than failing the Approve call itself, the
    /// same "primary write is authoritative, side effect is best-effort but never silently
    /// discarded" posture <c>CreateStudentRecordService</c> already established.
    /// </summary>
    private async Task TryFulfillAsync(StudentRequest request, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            switch (request.RequestType)
            {
                case StudentRequestType.Grievance:
                    request.Fulfill(null, clock.UtcNow);
                    break;

                case StudentRequestType.IdReissue:
                    await FulfillIdReissueAsync(request, correlationId, cancellationToken).ConfigureAwait(false);
                    break;

                case StudentRequestType.TranscriptRequest:
                    await FulfillTranscriptRequestAsync(request, correlationId, cancellationToken).ConfigureAwait(false);
                    break;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Fulfillment attempt failed for StudentRequest {StudentRequestId} - it remains Approved (fulfillment-pending) for manual/operator retry [correlationId={CorrelationId}].", request.Id.Value, correlationId);
        }
    }

    private async Task FulfillIdReissueAsync(StudentRequest request, string correlationId, CancellationToken cancellationToken)
    {
        var student = await students.GetByIdAsync(new StudentId(request.StudentId), cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            logger.LogWarning("StudentRequest {StudentRequestId} approved but its Student {StudentId} no longer exists - fulfillment skipped.", request.Id.Value, request.StudentId);
            return;
        }

        var outcome = await documentGeneration.RequestIdCardAsync(
            new RequestStudentIdCardRequest(student.Id.Value, student.StudentNumber.Value, student.Name.DisplayName, request.DecidedByUserId, correlationId),
            cancellationToken).ConfigureAwait(false);

        if (outcome.Succeeded)
        {
            request.Fulfill(outcome.DocumentId, clock.UtcNow);
        }
        else if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("ID-card reissue generation did not succeed for StudentRequest {StudentRequestId}: {Reason} [correlationId={CorrelationId}].", request.Id.Value, outcome.FailureReason, correlationId);
        }
    }

    private async Task FulfillTranscriptRequestAsync(StudentRequest request, string correlationId, CancellationToken cancellationToken)
    {
        var student = await students.GetByIdAsync(new StudentId(request.StudentId), cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            logger.LogWarning("StudentRequest {StudentRequestId} approved but its Student {StudentId} no longer exists - fulfillment skipped.", request.Id.Value, request.StudentId);
            return;
        }

        var transcript = await transcriptPort.GetTranscriptAsync(student.Id.Value, cancellationToken).ConfigureAwait(false);
        var fields = new Dictionary<string, string>
        {
            ["studentNumber"] = student.StudentNumber.Value,
            ["displayName"] = student.Name.DisplayName,
            ["rowCount"] = transcript.Rows.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["overallAverage"] = transcript.OverallAverage?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            ["rows"] = JsonSerializer.Serialize(transcript.Rows),
        };

        var outcome = await documentGeneration.RequestTranscriptAsync(
            new RequestStudentTranscriptRequest(student.Id.Value, request.Id.Value, student.Name.DisplayName, fields, request.DecidedByUserId, correlationId),
            cancellationToken).ConfigureAwait(false);

        if (outcome.Succeeded)
        {
            request.Fulfill(outcome.DocumentId, clock.UtcNow);
        }
        else if (logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning("Transcript PDF generation did not succeed for StudentRequest {StudentRequestId}: {Reason} [correlationId={CorrelationId}].", request.Id.Value, outcome.FailureReason, correlationId);
        }
    }

    /// <summary>Best-effort - STU-11's "relevant Department Head queue" (§3's event-consumer table). Only Grievance requests with a resolved scope get a directed notification; ID reissue/transcript and a scope-less (fully-escalated-to-Registrar) Grievance rely on Registrar/Admin's own dashboard instead of a per-request push.</summary>
    private async Task NotifyReviewersBestEffortAsync(StudentRequest request, string correlationId, CancellationToken cancellationToken)
    {
        if (request.RequestType != StudentRequestType.Grievance || request.ReviewScopeNodeId is not { } scopeNodeId)
        {
            return;
        }

        try
        {
            var reviewerUserIds = await reviewerScopeDirectory.GetUserIdsWithPermissionAtScopeAsync(StudentPermissions.RequestReview, scopeNodeId, cancellationToken).ConfigureAwait(false);
            foreach (var reviewerUserId in reviewerUserIds)
            {
                await notifications.PublishAsync(
                    new NotificationRequest(
                        reviewerUserId,
                        "StudentRequestSubmitted",
                        request.Id.Value.ToString(),
                        new Dictionary<string, string> { ["requestType"] = request.RequestType.ToString() },
                        DedupeKey: $"{request.Id.Value}:{reviewerUserId}"),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Reviewer NotificationRequest publish failed for StudentRequest {StudentRequestId} - the request itself is unaffected [correlationId={CorrelationId}].", request.Id.Value, correlationId);
        }
    }

    private async Task<bool> IsOwnerOrAuthorizedReviewerAsync(StudentRequest request, Guid callerUserId, bool callerHasReviewPermission, CancellationToken cancellationToken)
    {
        var owner = await students.GetByIdAsync(new StudentId(request.StudentId), cancellationToken).ConfigureAwait(false);
        if (owner?.IdentityUserId == callerUserId)
        {
            return true;
        }

        return callerHasReviewPermission && await IsAuthorizedReviewerAsync(request, callerUserId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The bare `studentrequest.review` permission is checked by the caller (endpoint `RequirePermission`) BEFORE this ever runs - this only adds the ADR-0006 scope check for a request routed to a specific Department/Faculty.</summary>
    private async Task<bool> IsAuthorizedReviewerAsync(StudentRequest request, Guid reviewerUserId, CancellationToken cancellationToken)
    {
        if (request.ReviewScopeNodeId is not { } scopeNodeId)
        {
            // No specific scope to check against (ID reissue/transcript, or a Grievance escalated
            // all the way past Faculty with nothing to route to) - bare permission suffices.
            return true;
        }

        return await reviewerScopeDirectory.HasPermissionAtScopeAsync(reviewerUserId, StudentPermissions.RequestReview, scopeNodeId, cancellationToken).ConfigureAwait(false);
    }
}
