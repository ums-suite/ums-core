using UMS.Modules.Faculty.Domain.Common;
using UMS.Modules.Faculty.Domain.Events;
using UMS.Modules.Faculty.Domain.Translations;

namespace UMS.Modules.Faculty.Domain.LeaveRequests;

/// <summary>
/// FAC-6: the approval-chain aggregate root (requirement-spec.md faculty §2 Leave Request, §3,
/// §4). Chain: <c>Draft -&gt; Submitted -&gt; DeptHeadApproved -&gt; Approved/Rejected/Cancelled</c>,
/// matching `srs1.md` §10.4's fixed Faculty -&gt; Department Head -&gt; Authorized Authority order.
///
/// <para>
/// design-decisions.md, "Self-Approval Routing Enforcement Mechanism": the reroute is persisted
/// at <see cref="Submit"/> time via <see cref="RoutedDirectlyToAuthority"/> - a self-authored
/// request is addressed to the Authorized Authority from the moment it becomes <c>Submitted</c>,
/// so no Department-Head queue query can ever surface it. The Application layer adds an
/// independent "approver != requester" check at the approve endpoint as defense-in-depth
/// (edge-cases.md).
/// </para>
///
/// <para>
/// design-decisions.md, "LeaveRequest State-Machine Concurrency Control": every state-changing
/// method here is meant to be called under an optimistic <c>xmin</c> version check
/// (<c>IUnitOfWork.SetExpectedVersion</c>) applied uniformly by the caller - submit, approve,
/// reject, and cancel alike race the same aggregate instance.
/// </para>
/// </summary>
public sealed class LeaveRequest : AggregateRoot<LeaveRequestId>
{
    private readonly List<TextTranslation> _reasonTranslations = [];

    private LeaveRequest()
    {
    }

    private LeaveRequest(LeaveRequestId id, Guid facultyMemberId, Guid requesterUserId, DateOnly startDate, DateOnly endDate, string reason, DateTimeOffset now)
    {
        Id = id;
        FacultyMemberId = facultyMemberId;
        RequesterUserId = requesterUserId;
        StartDate = startDate;
        EndDate = endDate;
        Reason = reason;
        Status = LeaveRequestStatus.Draft;
        CreatedAt = now;
    }

    public Guid FacultyMemberId { get; private set; }

    public Guid RequesterUserId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public IReadOnlyCollection<TextTranslation> ReasonTranslations => _reasonTranslations.AsReadOnly();

    public LeaveRequestStatus Status { get; private set; }

    /// <summary>Persisted at <see cref="Submit"/> time - see class remarks.</summary>
    public bool RoutedDirectlyToAuthority { get; private set; }

    public Guid? SupportingDocumentReference { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public static LeaveRequest CreateDraft(Guid facultyMemberId, Guid requesterUserId, DateOnly startDate, DateOnly endDate, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Leave reason is required.", nameof(reason));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("End date cannot be before start date.", nameof(endDate));
        }

        return new LeaveRequest(LeaveRequestId.New(), facultyMemberId, requesterUserId, startDate, endDate, reason.Trim(), now);
    }

    public void SetReasonTranslation(string languageCode, string text)
    {
        var translation = TextTranslation.Create(languageCode, text);
        var existingIndex = _reasonTranslations.FindIndex(t => string.Equals(t.LanguageCode, translation.LanguageCode, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _reasonTranslations[existingIndex].UpdateText(translation.Text);
        }
        else
        {
            _reasonTranslations.Add(translation);
        }
    }

    public string ResolveReason(string? languageCode) => TextLocalizer.Resolve(Reason, _reasonTranslations, languageCode);

    public void AttachSupportingDocument(Guid generatedDocumentId)
    {
        if (Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Submitted or LeaveRequestStatus.DeptHeadApproved))
        {
            throw new InvalidOperationException($"Cannot attach a supporting document to a LeaveRequest in status {Status}.");
        }

        SupportingDocumentReference = generatedDocumentId;
    }

    /// <summary><paramref name="requesterIsDepartmentHead"/> is resolved by the caller from the requester's own FacultyMember profile (§4/§8's documented exception).</summary>
    public void Submit(bool requesterIsDepartmentHead, DateTimeOffset now)
    {
        if (Status != LeaveRequestStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot submit a LeaveRequest in status {Status} - only a Draft may be submitted.");
        }

        Status = LeaveRequestStatus.Submitted;
        RoutedDirectlyToAuthority = requesterIsDepartmentHead;
        SubmittedAt = now;
        Raise(new LeaveRequested(Id.Value, FacultyMemberId, RoutedDirectlyToAuthority, now));
    }

    public void ApproveByDepartmentHead(Guid approverUserId, DateTimeOffset now)
    {
        if (Status != LeaveRequestStatus.Submitted || RoutedDirectlyToAuthority)
        {
            throw new InvalidOperationException("Only a Submitted, non-rerouted LeaveRequest may be approved at the Department Head step.");
        }

        Status = LeaveRequestStatus.DeptHeadApproved;
        Raise(new LeaveApprovedByDepartmentHead(Id.Value, FacultyMemberId, approverUserId, now));
    }

    public void ApproveByAuthority(Guid approverUserId, DateTimeOffset now)
    {
        var eligible = Status == LeaveRequestStatus.DeptHeadApproved
            || (Status == LeaveRequestStatus.Submitted && RoutedDirectlyToAuthority);

        if (!eligible)
        {
            throw new InvalidOperationException($"LeaveRequest in status {Status} is not eligible for Authorized Authority approval.");
        }

        Status = LeaveRequestStatus.Approved;
        DecidedAt = now;
        Raise(new LeaveApproved(Id.Value, FacultyMemberId, RequesterUserId, approverUserId, now));
    }

    public void Reject(Guid approverUserId, string? reason, DateTimeOffset now)
    {
        if (Status is not (LeaveRequestStatus.Submitted or LeaveRequestStatus.DeptHeadApproved))
        {
            throw new InvalidOperationException($"Cannot reject a LeaveRequest in status {Status}.");
        }

        Status = LeaveRequestStatus.Rejected;
        DecidedAt = now;
        Raise(new LeaveRejected(Id.Value, FacultyMemberId, RequesterUserId, approverUserId, reason, now));
    }

    /// <summary>"While still pending" (requirement-spec.md §2) - Draft, Submitted, or DeptHeadApproved, never an already-decided request.</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status is not (LeaveRequestStatus.Draft or LeaveRequestStatus.Submitted or LeaveRequestStatus.DeptHeadApproved))
        {
            throw new InvalidOperationException($"Cannot cancel a LeaveRequest in status {Status} - it is no longer pending.");
        }

        Status = LeaveRequestStatus.Cancelled;
        DecidedAt = now;
        Raise(new LeaveCancelled(Id.Value, FacultyMemberId, now));
    }
}
