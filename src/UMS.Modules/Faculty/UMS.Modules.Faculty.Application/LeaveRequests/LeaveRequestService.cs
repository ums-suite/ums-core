using System.Text.Json;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Application.Common;
using UMS.Modules.Faculty.Domain.FacultyMembers;
using UMS.Modules.Faculty.Domain.LeaveRequests;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.Application.LeaveRequests;

/// <summary>
/// FAC-6..FAC-11: the `LeaveRequest` approval-chain workflow (requirement-spec.md faculty §2 Leave
/// Request, §4, §8). Endpoints split the spec's single literal `.../approve` and `.../reject`
/// routes into a department-head step and an authority step (see <c>LeaveRequestEndpoints</c>'s own
/// remarks) so each step can be gated by its own permission string (§2's Permission Strings) rather
/// than one endpoint silently accepting either actor - a documented deviation from the literal
/// endpoint table, in the same spirit as Notifications' own template-create endpoint addition
/// (release/DEVELOPMENT_PLAN.md Flow #8's PR notes).
/// </summary>
public sealed class LeaveRequestService(
    ILeaveRequestRepository leaveRequests,
    IFacultyMemberRepository facultyMembers,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static LeaveRequestDto ToDto(LeaveRequest leaveRequest, string? languageCode) => new(
        leaveRequest.Id.Value,
        leaveRequest.FacultyMemberId,
        leaveRequest.RequesterUserId,
        leaveRequest.StartDate,
        leaveRequest.EndDate,
        leaveRequest.Reason,
        leaveRequest.ResolveReason(languageCode),
        leaveRequest.Status.ToString(),
        leaveRequest.RoutedDirectlyToAuthority,
        leaveRequest.SupportingDocumentReference,
        leaveRequest.CreatedAt,
        leaveRequest.SubmittedAt,
        leaveRequest.DecidedAt,
        leaveRequest.Version);

    /// <summary>edge-cases.md "Department Head Applies for Leave While Their Own Approval Queue Is Open": the reroute is resolved and persisted here, at submission, from the requester's own <see cref="FacultyMember.IsDepartmentHead"/> flag.</summary>
    public async Task<Result<LeaveRequestDto>> SubmitAsync(Guid callerUserId, SubmitLeaveRequestRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var facultyMember = await facultyMembers.GetByIdAsync(new FacultyMemberId(request.FacultyMemberId), cancellationToken).ConfigureAwait(false);
        if (facultyMember is null)
        {
            return Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{request.FacultyMemberId}'.");
        }

        if (facultyMember.UserId != callerUserId)
        {
            return Error.Forbidden("leaverequest.not_owner", "Only the owning FacultyMember may submit their own LeaveRequest.");
        }

        Result<LeaveRequestDto> result;
        try
        {
            var now = clock.UtcNow;
            var leaveRequest = LeaveRequest.CreateDraft(facultyMember.Id.Value, callerUserId, request.StartDate, request.EndDate, request.Reason, now);
            ApplyReasonTranslations(leaveRequest, request.ReasonTranslations);
            leaveRequest.Submit(facultyMember.IsDepartmentHead, now);
            leaveRequests.Add(leaveRequest);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = audit.ToRequest(
                "LeaveRequest",
                leaveRequest.Id.Value.ToString(),
                AuditActions.Create,
                null,
                JsonSerializer.Serialize(new { status = leaveRequest.Status.ToString(), routedDirectlyToAuthority = leaveRequest.RoutedDirectlyToAuthority }));

            var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            result = commitResult.IsFailure ? commitResult.Error! : ToDto(leaveRequest, null);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("leaverequest.invalid", ex.Message);
        }

        return result;
    }

    public async Task<Result<LeaveRequestDto>> ApproveByDepartmentHeadAsync(Guid id, Guid approverUserId, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await leaveRequests.GetByIdAsync(new LeaveRequestId(id), cancellationToken).ConfigureAwait(false);
        if (leaveRequest is null)
        {
            return Error.NotFound("leaverequest.not_found", $"No LeaveRequest exists with id '{id}'.");
        }

        if (leaveRequest.Status != LeaveRequestStatus.Submitted || leaveRequest.RoutedDirectlyToAuthority)
        {
            return Error.Conflict("leaverequest.not_eligible_department_step", "This LeaveRequest is not awaiting Department Head approval.");
        }

        // edge-cases.md "Department Head Applies for Leave While Their Own Approval Queue Is
        // Open": defense-in-depth re-check, on top of the routing already persisted at Submit.
        if (approverUserId == leaveRequest.RequesterUserId)
        {
            return Error.Forbidden("leaverequest.self_approval_forbidden", "A Department Head cannot approve their own LeaveRequest.");
        }

        return await TransitionAsync(leaveRequest, expectedVersion, audit, "approve_department_head", () => leaveRequest.ApproveByDepartmentHead(approverUserId, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LeaveRequestDto>> ApproveByAuthorityAsync(Guid id, Guid approverUserId, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await leaveRequests.GetByIdAsync(new LeaveRequestId(id), cancellationToken).ConfigureAwait(false);
        if (leaveRequest is null)
        {
            return Error.NotFound("leaverequest.not_found", $"No LeaveRequest exists with id '{id}'.");
        }

        var eligible = leaveRequest.Status == LeaveRequestStatus.DeptHeadApproved
            || (leaveRequest.Status == LeaveRequestStatus.Submitted && leaveRequest.RoutedDirectlyToAuthority);
        if (!eligible)
        {
            return Error.Conflict("leaverequest.not_eligible_authority_step", "This LeaveRequest is not awaiting Authorized Authority approval.");
        }

        if (approverUserId == leaveRequest.RequesterUserId)
        {
            return Error.Forbidden("leaverequest.self_approval_forbidden", "The Authorized Authority cannot approve their own LeaveRequest.");
        }

        return await TransitionAsync(leaveRequest, expectedVersion, audit, AuditActions.Approve, () => leaveRequest.ApproveByAuthority(approverUserId, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LeaveRequestDto>> RejectAsync(Guid id, Guid approverUserId, RejectLeaveRequestRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await leaveRequests.GetByIdAsync(new LeaveRequestId(id), cancellationToken).ConfigureAwait(false);
        if (leaveRequest is null)
        {
            return Error.NotFound("leaverequest.not_found", $"No LeaveRequest exists with id '{id}'.");
        }

        if (approverUserId == leaveRequest.RequesterUserId)
        {
            return Error.Forbidden("leaverequest.self_approval_forbidden", "An approver cannot reject their own LeaveRequest.");
        }

        // Audit's own AuditLogEntry.Create rejects a `reject` action outright without a Reason
        // (requirement-spec.md audit §4: "Reason is mandatory for reversal-shaped actions") -
        // validated here too so the caller gets a Faculty-specific error rather than a generic
        // Audit one.
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("leaverequest.reject_reason_required", "A reason is required to reject a LeaveRequest.");
        }

        return await TransitionAsync(leaveRequest, request.Version, audit, AuditActions.Reject, () => leaveRequest.Reject(approverUserId, request.Reason, clock.UtcNow), cancellationToken, request.Reason).ConfigureAwait(false);
    }

    /// <summary>edge-cases.md "LeaveRequest Cancellation Racing an In-Flight Approval Decision": no special-cased precedence, just the same version-checked transition every other endpoint uses.</summary>
    public async Task<Result<LeaveRequestDto>> CancelAsync(Guid id, Guid callerUserId, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await leaveRequests.GetByIdAsync(new LeaveRequestId(id), cancellationToken).ConfigureAwait(false);
        if (leaveRequest is null)
        {
            return Error.NotFound("leaverequest.not_found", $"No LeaveRequest exists with id '{id}'.");
        }

        if (leaveRequest.RequesterUserId != callerUserId)
        {
            return Error.Forbidden("leaverequest.not_owner", "Only the requester may cancel their own LeaveRequest.");
        }

        return await TransitionAsync(leaveRequest, expectedVersion, audit, "cancel", () => leaveRequest.Cancel(clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<LeaveRequestDto>> AttachSupportingDocumentAsync(Guid id, Guid callerUserId, AttachSupportingDocumentRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var leaveRequest = await leaveRequests.GetByIdAsync(new LeaveRequestId(id), cancellationToken).ConfigureAwait(false);
        if (leaveRequest is null)
        {
            return Error.NotFound("leaverequest.not_found", $"No LeaveRequest exists with id '{id}'.");
        }

        if (leaveRequest.RequesterUserId != callerUserId)
        {
            return Error.Forbidden("leaverequest.not_owner", "Only the requester may attach a supporting document to their own LeaveRequest.");
        }

        return await TransitionAsync(leaveRequest, request.Version, audit, "attach_document", () => leaveRequest.AttachSupportingDocument(request.GeneratedDocumentId), cancellationToken).ConfigureAwait(false);
    }

    public async Task<LeaveRequestListPage> ListByFacultyMemberAsync(Guid facultyMemberId, int skip, int take, string? languageCode, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var items = await leaveRequests.ListByFacultyMemberAsync(facultyMemberId, skip, take, cancellationToken).ConfigureAwait(false);
        return new LeaveRequestListPage(items.Select(r => ToDto(r, languageCode)).ToList(), skip, take);
    }

    private static void ApplyReasonTranslations(LeaveRequest leaveRequest, IReadOnlyDictionary<string, string>? translations)
    {
        if (translations is null)
        {
            return;
        }

        foreach (var (languageCode, text) in translations)
        {
            leaveRequest.SetReasonTranslation(languageCode, text);
        }
    }

    private async Task<Result<LeaveRequestDto>> TransitionAsync(LeaveRequest leaveRequest, uint expectedVersion, AuditContext audit, string action, Action transition, CancellationToken cancellationToken, string? auditReason = null)
    {
        var beforeStatus = leaveRequest.Status.ToString();

        try
        {
            transition();
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("leaverequest.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(leaveRequest, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "LeaveRequest",
            leaveRequest.Id.Value.ToString(),
            action,
            JsonSerializer.Serialize(new { status = beforeStatus }),
            JsonSerializer.Serialize(new { status = leaveRequest.Status.ToString() }),
            auditReason);

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(leaveRequest, null);
    }
}
