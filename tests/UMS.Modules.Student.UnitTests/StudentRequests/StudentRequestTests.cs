using UMS.Modules.Student.Domain.Events;
using UMS.Modules.Student.Domain.StudentRequests;

namespace UMS.Modules.Student.UnitTests.StudentRequests;

/// <summary>STU-9..STU-14 (requirement-spec.md student §2/§4; design-decisions.md "StudentRequest Dedup Mechanism").</summary>
public sealed class StudentRequestTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void SubmitIdReissue_creates_a_Submitted_request_and_raises_StudentRequestSubmitted()
    {
        var request = StudentRequest.SubmitIdReissue(Guid.NewGuid(), "Lost my card", Now);

        Assert.Equal(StudentRequestType.IdReissue, request.RequestType);
        Assert.Equal(StudentRequestStatus.Submitted, request.Status);
        Assert.Equal("Lost my card", request.Details);
        Assert.Null(request.ReviewScopeNodeId);
        var domainEvent = Assert.Single(request.DomainEvents);
        Assert.IsType<StudentRequestSubmitted>(domainEvent);
    }

    [Fact]
    public void SubmitIdReissue_with_blank_reason_throws()
    {
        Assert.Throws<ArgumentException>(() => StudentRequest.SubmitIdReissue(Guid.NewGuid(), "   ", Now));
    }

    [Fact]
    public void SubmitTranscriptRequest_creates_a_Submitted_request()
    {
        var request = StudentRequest.SubmitTranscriptRequest(Guid.NewGuid(), "Job application", Now);

        Assert.Equal(StudentRequestType.TranscriptRequest, request.RequestType);
        Assert.Equal(StudentRequestStatus.Submitted, request.Status);
    }

    [Fact]
    public void SubmitGrievance_carries_the_resolved_scope_and_escalation_flag()
    {
        var facultyScopeId = Guid.NewGuid();
        var request = StudentRequest.SubmitGrievance(Guid.NewGuid(), "My department head is unfair", facultyScopeId, isAgainstOwnDepartmentHead: true, Now);

        Assert.Equal(StudentRequestType.Grievance, request.RequestType);
        Assert.Equal(facultyScopeId, request.ReviewScopeNodeId);
        Assert.True(request.IsAgainstOwnDepartmentHead);
    }

    [Fact]
    public void Approve_from_Submitted_transitions_to_Approved_and_raises_StudentRequestApproved()
    {
        var request = StudentRequest.SubmitIdReissue(Guid.NewGuid(), "Lost card", Now);
        request.ClearDomainEvents();
        var approverId = Guid.NewGuid();

        request.Approve(approverId, Now);

        Assert.Equal(StudentRequestStatus.Approved, request.Status);
        Assert.Equal(approverId, request.DecidedByUserId);
        Assert.NotNull(request.DecidedAt);
        Assert.Contains(request.DomainEvents, e => e is StudentRequestApproved);
    }

    [Fact]
    public void Approve_a_terminal_request_throws()
    {
        var request = StudentRequest.SubmitIdReissue(Guid.NewGuid(), "Lost card", Now);
        request.Reject(Guid.NewGuid(), "Not eligible", Now);

        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Reject_requires_a_non_blank_reason()
    {
        var request = StudentRequest.SubmitIdReissue(Guid.NewGuid(), "Lost card", Now);

        Assert.Throws<ArgumentException>(() => request.Reject(Guid.NewGuid(), "  ", Now));
    }

    [Fact]
    public void Reject_from_Submitted_transitions_to_Rejected_and_raises_StudentRequestRejected()
    {
        var request = StudentRequest.SubmitIdReissue(Guid.NewGuid(), "Lost card", Now);
        request.ClearDomainEvents();

        request.Reject(Guid.NewGuid(), "Insufficient evidence", Now);

        Assert.Equal(StudentRequestStatus.Rejected, request.Status);
        Assert.Equal("Insufficient evidence", request.DecisionReason);
        Assert.Contains(request.DomainEvents, e => e is StudentRequestRejected);
    }

    [Fact]
    public void Fulfill_before_Approve_throws()
    {
        var request = StudentRequest.SubmitIdReissue(Guid.NewGuid(), "Lost card", Now);

        Assert.Throws<InvalidOperationException>(() => request.Fulfill(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Fulfill_after_Approve_transitions_to_Fulfilled_and_raises_StudentRequestFulfilled()
    {
        var request = StudentRequest.SubmitIdReissue(Guid.NewGuid(), "Lost card", Now);
        request.Approve(Guid.NewGuid(), Now);
        request.ClearDomainEvents();
        var documentId = Guid.NewGuid();

        request.Fulfill(documentId, Now);

        Assert.Equal(StudentRequestStatus.Fulfilled, request.Status);
        Assert.Equal(documentId, request.GeneratedDocumentId);
        Assert.NotNull(request.FulfilledAt);
        Assert.Contains(request.DomainEvents, e => e is StudentRequestFulfilled);
    }

    [Fact]
    public void Fulfill_for_a_Grievance_accepts_a_null_document_id()
    {
        var request = StudentRequest.SubmitGrievance(Guid.NewGuid(), "Complaint", reviewScopeNodeId: Guid.NewGuid(), isAgainstOwnDepartmentHead: false, Now);
        request.Approve(Guid.NewGuid(), Now);

        request.Fulfill(null, Now);

        Assert.Equal(StudentRequestStatus.Fulfilled, request.Status);
        Assert.Null(request.GeneratedDocumentId);
    }
}
