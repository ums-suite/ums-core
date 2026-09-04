using UMS.Modules.Faculty.Domain.LeaveRequests;

namespace UMS.Modules.Faculty.UnitTests.LeaveRequests;

public sealed class LeaveRequestTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static LeaveRequest CreateSubmitted(bool requesterIsDepartmentHead = false)
    {
        var leaveRequest = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), "Personal", Now);
        leaveRequest.Submit(requesterIsDepartmentHead, Now);
        return leaveRequest;
    }

    [Fact]
    public void Submit_moves_Draft_to_Submitted()
    {
        var leaveRequest = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), "Personal", Now);

        leaveRequest.Submit(requesterIsDepartmentHead: false, Now);

        Assert.Equal(LeaveRequestStatus.Submitted, leaveRequest.Status);
        Assert.False(leaveRequest.RoutedDirectlyToAuthority);
    }

    [Fact]
    public void Submit_by_a_department_head_routes_directly_to_authority()
    {
        var leaveRequest = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), "Personal", Now);

        leaveRequest.Submit(requesterIsDepartmentHead: true, Now);

        Assert.True(leaveRequest.RoutedDirectlyToAuthority);
    }

    [Fact]
    public void ApproveByDepartmentHead_on_a_rerouted_request_throws()
    {
        var leaveRequest = CreateSubmitted(requesterIsDepartmentHead: true);

        Assert.Throws<InvalidOperationException>(() => leaveRequest.ApproveByDepartmentHead(Guid.NewGuid(), Now));
    }

    [Fact]
    public void ApproveByAuthority_on_a_rerouted_Submitted_request_succeeds_directly()
    {
        var leaveRequest = CreateSubmitted(requesterIsDepartmentHead: true);

        leaveRequest.ApproveByAuthority(Guid.NewGuid(), Now);

        Assert.Equal(LeaveRequestStatus.Approved, leaveRequest.Status);
    }

    [Fact]
    public void ApproveByAuthority_before_DeptHeadApproved_on_a_non_rerouted_request_throws()
    {
        var leaveRequest = CreateSubmitted(requesterIsDepartmentHead: false);

        Assert.Throws<InvalidOperationException>(() => leaveRequest.ApproveByAuthority(Guid.NewGuid(), Now));
    }

    [Fact]
    public void Full_chain_DeptHeadApproved_then_Approved()
    {
        var leaveRequest = CreateSubmitted();

        leaveRequest.ApproveByDepartmentHead(Guid.NewGuid(), Now);
        Assert.Equal(LeaveRequestStatus.DeptHeadApproved, leaveRequest.Status);

        leaveRequest.ApproveByAuthority(Guid.NewGuid(), Now);
        Assert.Equal(LeaveRequestStatus.Approved, leaveRequest.Status);
    }

    [Fact]
    public void Reject_from_Submitted_succeeds()
    {
        var leaveRequest = CreateSubmitted();

        leaveRequest.Reject(Guid.NewGuid(), "No balance", Now);

        Assert.Equal(LeaveRequestStatus.Rejected, leaveRequest.Status);
    }

    [Fact]
    public void Reject_an_already_Approved_request_throws()
    {
        var leaveRequest = CreateSubmitted();
        leaveRequest.ApproveByDepartmentHead(Guid.NewGuid(), Now);
        leaveRequest.ApproveByAuthority(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => leaveRequest.Reject(Guid.NewGuid(), null, Now));
    }

    [Fact]
    public void Cancel_while_Submitted_succeeds()
    {
        var leaveRequest = CreateSubmitted();

        leaveRequest.Cancel(Now);

        Assert.Equal(LeaveRequestStatus.Cancelled, leaveRequest.Status);
    }

    [Fact]
    public void Cancel_an_already_Approved_request_throws()
    {
        var leaveRequest = CreateSubmitted();
        leaveRequest.ApproveByDepartmentHead(Guid.NewGuid(), Now);
        leaveRequest.ApproveByAuthority(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => leaveRequest.Cancel(Now));
    }

    [Fact]
    public void AttachSupportingDocument_after_a_decision_throws()
    {
        var leaveRequest = CreateSubmitted();
        leaveRequest.ApproveByDepartmentHead(Guid.NewGuid(), Now);
        leaveRequest.ApproveByAuthority(Guid.NewGuid(), Now);

        Assert.Throws<InvalidOperationException>(() => leaveRequest.AttachSupportingDocument(Guid.NewGuid()));
    }

    [Fact]
    public void ResolveReason_falls_back_to_English_when_no_translation_exists()
    {
        var leaveRequest = LeaveRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 3), "Personal matter", Now);

        Assert.Equal("Personal matter", leaveRequest.ResolveReason("bn"));

        leaveRequest.SetReasonTranslation("bn", "ব্যক্তিগত বিষয়");

        Assert.Equal("ব্যক্তিগত বিষয়", leaveRequest.ResolveReason("bn"));
        Assert.Equal("Personal matter", leaveRequest.ResolveReason(null));
    }
}
