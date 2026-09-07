using UMS.Modules.Student.Domain.Common;
using UMS.Modules.Student.Domain.Events;

namespace UMS.Modules.Student.Domain.StudentRequests;

/// <summary>
/// STU-9..STU-14: the Student-initiated service-request aggregate root (requirement-spec.md
/// student §2 Student-Initiated Requests, §3, §4). A separate aggregate root from <c>Student</c>
/// (glossary: "StudentRequest ... own approval workflow") - not an owned child collection, since it
/// needs its own repository, its own partial-unique dedup index (design-decisions.md, "StudentRequest
/// Dedup Mechanism"), and independent optimistic concurrency from the (much larger, more heavily
/// read) <c>Student</c> row itself.
///
/// <para>
/// <b>One aggregate, three request shapes.</b> ID reissue, transcript request, and grievance share
/// the same lifecycle (<c>Submitted -&gt; UnderReview -&gt; Approved/Rejected -&gt; Fulfilled</c>) and
/// the same dedup/Suspended-student rules (§4, §8), so they are modeled as one aggregate
/// distinguished by <see cref="RequestType"/> rather than three separate aggregate types - the
/// alternative (three near-identical aggregates) would triplicate every one of those shared
/// invariants. <see cref="Details"/> carries the one piece of free-text data every type needs (the
/// reissue reason / transcript purpose / grievance description); <see cref="ReviewScopeNodeId"/> and
/// <see cref="IsAgainstOwnDepartmentHead"/> are meaningful only for <see cref="StudentRequestType.Grievance"/>
/// (edge-cases.md, "A grievance is filed against a Department Head's own department").
/// </para>
/// </summary>
public sealed class StudentRequest : AggregateRoot<StudentRequestId>
{
    private StudentRequest()
    {
    }

    private StudentRequest(
        StudentRequestId id,
        Guid studentId,
        StudentRequestType requestType,
        string details,
        Guid? reviewScopeNodeId,
        bool isAgainstOwnDepartmentHead,
        DateTimeOffset now)
    {
        Id = id;
        StudentId = studentId;
        RequestType = requestType;
        Details = details;
        ReviewScopeNodeId = reviewScopeNodeId;
        IsAgainstOwnDepartmentHead = isAgainstOwnDepartmentHead;
        Status = StudentRequestStatus.Submitted;
        SubmittedAt = now;
    }

    public Guid StudentId { get; private set; }

    public StudentRequestType RequestType { get; private set; }

    /// <summary>The reissue reason / transcript purpose / grievance description - required, free text.</summary>
    public string Details { get; private set; } = string.Empty;

    public StudentRequestStatus Status { get; private set; }

    /// <summary>
    /// Grievance-only: the `Organization` node (Department, or its parent Faculty on escalation)
    /// whose reviewer(s) may see/decide this request (design-decisions.md, ADR-0006 ScopeGrant
    /// scoping). <see langword="null"/> for ID reissue/transcript requests (Registrar/Admin-wide
    /// visibility via the bare `studentrequest.review` permission, no extra scope check) and for a
    /// grievance whose escalation walk found no parent scope to route to (falls back to the same
    /// bare-permission, university-wide visibility).
    /// </summary>
    public Guid? ReviewScopeNodeId { get; private set; }

    /// <summary>Grievance-only, student-declared at submission (edge-cases.md's conflict-of-interest escalation trigger) - always <see langword="false"/> for the other two request types.</summary>
    public bool IsAgainstOwnDepartmentHead { get; private set; }

    /// <summary>Set on <see cref="Fulfill"/> for a document-backed request type (ID reissue/transcript); always <see langword="null"/> for a Grievance.</summary>
    public Guid? GeneratedDocumentId { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    /// <summary>Rejection reason (STU-14, mandatory) - never set on an Approve.</summary>
    public string? DecisionReason { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public DateTimeOffset? FulfilledAt { get; private set; }

    /// <summary>STU-9: rejected outright for a Suspended Student by the caller (edge-cases.md) before this factory ever runs - see <c>StudentRequestService</c>'s own remarks.</summary>
    public static StudentRequest SubmitIdReissue(Guid studentId, string reason, DateTimeOffset now) =>
        Create(studentId, StudentRequestType.IdReissue, reason, reviewScopeNodeId: null, isAgainstOwnDepartmentHead: false, now);

    /// <summary>STU-10: same Suspended-student restriction as <see cref="SubmitIdReissue"/>.</summary>
    public static StudentRequest SubmitTranscriptRequest(Guid studentId, string purpose, DateTimeOffset now) =>
        Create(studentId, StudentRequestType.TranscriptRequest, purpose, reviewScopeNodeId: null, isAgainstOwnDepartmentHead: false, now);

    /// <summary>
    /// STU-11: the one request type a Suspended Student may still file (edge-cases.md, §9 decision
    /// 6). <paramref name="reviewScopeNodeId"/> is resolved by the caller BEFORE this factory runs
    /// (the Student's own Department, or its parent Faculty if <paramref name="isAgainstOwnDepartmentHead"/>
    /// - <c>StudentRequestService</c>'s own remarks), since routing resolution needs a
    /// cross-module Organization lookup a pure domain factory must never perform itself.
    /// </summary>
    public static StudentRequest SubmitGrievance(Guid studentId, string description, Guid? reviewScopeNodeId, bool isAgainstOwnDepartmentHead, DateTimeOffset now) =>
        Create(studentId, StudentRequestType.Grievance, description, reviewScopeNodeId, isAgainstOwnDepartmentHead, now);

    /// <summary>STU-13 (Department Head/Registrar). Legal from <see cref="StudentRequestStatus.Submitted"/> or <see cref="StudentRequestStatus.UnderReview"/> only.</summary>
    public void Approve(Guid approverUserId, DateTimeOffset now)
    {
        RequireOpen();

        Status = StudentRequestStatus.Approved;
        DecidedByUserId = approverUserId;
        DecidedAt = now;
        Raise(new StudentRequestApproved(Id.Value, StudentId, approverUserId, now));
    }

    /// <summary>STU-14: reason is mandatory (requirement-spec.md §6 "reason required").</summary>
    public void Reject(Guid approverUserId, string reason, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to reject a StudentRequest.", nameof(reason));
        }

        RequireOpen();

        Status = StudentRequestStatus.Rejected;
        DecidedByUserId = approverUserId;
        DecisionReason = reason.Trim();
        DecidedAt = now;
        Raise(new StudentRequestRejected(Id.Value, StudentId, approverUserId, reason.Trim(), now));
    }

    /// <summary>
    /// STU-13's own "triggers fulfillment" half - legal only from <see cref="StudentRequestStatus.Approved"/>.
    /// For ID reissue/transcript, the caller (<c>StudentRequestService</c>) only reaches this after a
    /// successful Documents call; a failed Documents call leaves the request sitting in
    /// <see cref="StudentRequestStatus.Approved"/> (fulfillment-pending, operator-retryable) rather
    /// than calling this at all - the same "primary write is authoritative, side effect is
    /// best-effort but never silently discarded" posture <c>CreateStudentRecordService</c> already
    /// established. For a Grievance, the review decision IS the fulfillment - the caller invokes
    /// this immediately after <see cref="Approve"/> with a <see langword="null"/> document id.
    /// </summary>
    public void Fulfill(Guid? generatedDocumentId, DateTimeOffset now)
    {
        if (Status != StudentRequestStatus.Approved)
        {
            throw new InvalidOperationException($"Cannot fulfill a StudentRequest in status '{Status}' - it must be Approved first.");
        }

        Status = StudentRequestStatus.Fulfilled;
        GeneratedDocumentId = generatedDocumentId;
        FulfilledAt = now;
        Raise(new StudentRequestFulfilled(Id.Value, StudentId, generatedDocumentId, now));
    }

    private static StudentRequest Create(Guid studentId, StudentRequestType requestType, string details, Guid? reviewScopeNodeId, bool isAgainstOwnDepartmentHead, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            throw new ArgumentException("A StudentRequest requires non-empty details.", nameof(details));
        }

        var request = new StudentRequest(StudentRequestId.New(), studentId, requestType, details.Trim(), reviewScopeNodeId, isAgainstOwnDepartmentHead, now);
        request.Raise(new StudentRequestSubmitted(request.Id.Value, studentId, requestType.ToString(), now));
        return request;
    }

    private void RequireOpen()
    {
        if (Status is not (StudentRequestStatus.Submitted or StudentRequestStatus.UnderReview))
        {
            throw new InvalidOperationException($"Cannot decide a StudentRequest in status '{Status}' - it must be Submitted or UnderReview.");
        }
    }
}
