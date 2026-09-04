using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Application.Common;
using UMS.Modules.Faculty.Application.LeaveRequests;
using UMS.Modules.Faculty.Domain.FacultyMembers;
using UMS.Modules.Faculty.UnitTests.TestDoubles;

namespace UMS.Modules.Faculty.UnitTests.LeaveRequests;

/// <summary>edge-cases.md "Department Head Applies for Leave While Their Own Approval Queue Is Open": the Application-layer defense-in-depth half of the two-layer enforcement (design-decisions.md).</summary>
public sealed class LeaveRequestServiceTests
{
    private static AuditContext Audit(Guid actorUserId) => new(actorUserId, "127.0.0.1", Guid.NewGuid().ToString());

    private static (LeaveRequestService Service, FakeLeaveRequestRepository LeaveRequests, FakeFacultyMemberRepository FacultyMembers) CreateService()
    {
        var leaveRequests = new FakeLeaveRequestRepository();
        var facultyMembers = new FakeFacultyMemberRepository();
        var service = new LeaveRequestService(leaveRequests, facultyMembers, new FakeUnitOfWork(), new FakeAuditRecorder(), new FakeClock());
        return (service, leaveRequests, facultyMembers);
    }

    [Fact]
    public async Task SubmitAsync_by_someone_other_than_the_owning_FacultyMember_is_forbidden()
    {
        var (service, _, facultyMembers) = CreateService();
        var facultyMember = FacultyMember.Onboard(Guid.NewGuid(), "EMP-1", Guid.NewGuid(), Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), DateTimeOffset.UtcNow);
        facultyMembers.Seed(facultyMember);

        var result = await service.SubmitAsync(Guid.NewGuid(), new SubmitLeaveRequestRequest(facultyMember.Id.Value, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), "Trip", null), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("leaverequest.not_owner", result.Error!.Code);
    }

    [Fact]
    public async Task ApproveByDepartmentHead_when_the_approver_is_the_requester_is_forbidden_even_if_not_rerouted()
    {
        var (service, leaveRequests, facultyMembers) = CreateService();
        var requesterUserId = Guid.NewGuid();
        var facultyMember = FacultyMember.Onboard(requesterUserId, "EMP-2", Guid.NewGuid(), Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), DateTimeOffset.UtcNow);
        facultyMembers.Seed(facultyMember);

        var submitResult = await service.SubmitAsync(requesterUserId, new SubmitLeaveRequestRequest(facultyMember.Id.Value, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), "Trip", null), Audit(requesterUserId));
        Assert.True(submitResult.IsSuccess);

        // Even though this request was never marked RoutedDirectlyToAuthority, the requester
        // themself attempting the department-head approval must still be rejected - defense in
        // depth, not solely reliant on the persisted-at-submission routing flag.
        var approveResult = await service.ApproveByDepartmentHeadAsync(submitResult.Value.Id, requesterUserId, submitResult.Value.Version, Audit(requesterUserId));

        Assert.True(approveResult.IsFailure);
        Assert.Equal("leaverequest.self_approval_forbidden", approveResult.Error!.Code);
    }

    [Fact]
    public async Task RejectAsync_when_the_approver_is_the_requester_is_forbidden()
    {
        var (service, leaveRequests, facultyMembers) = CreateService();
        var requesterUserId = Guid.NewGuid();
        var facultyMember = FacultyMember.Onboard(requesterUserId, "EMP-3", Guid.NewGuid(), Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), DateTimeOffset.UtcNow);
        facultyMembers.Seed(facultyMember);
        var submitResult = await service.SubmitAsync(requesterUserId, new SubmitLeaveRequestRequest(facultyMember.Id.Value, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), "Trip", null), Audit(requesterUserId));

        var rejectResult = await service.RejectAsync(submitResult.Value.Id, requesterUserId, new RejectLeaveRequestRequest(null, submitResult.Value.Version), Audit(requesterUserId));

        Assert.True(rejectResult.IsFailure);
        Assert.Equal("leaverequest.self_approval_forbidden", rejectResult.Error!.Code);
    }

    [Fact]
    public async Task CancelAsync_by_a_non_requester_is_forbidden()
    {
        var (service, _, facultyMembers) = CreateService();
        var requesterUserId = Guid.NewGuid();
        var facultyMember = FacultyMember.Onboard(requesterUserId, "EMP-4", Guid.NewGuid(), Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), DateTimeOffset.UtcNow);
        facultyMembers.Seed(facultyMember);
        var submitResult = await service.SubmitAsync(requesterUserId, new SubmitLeaveRequestRequest(facultyMember.Id.Value, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 2), "Trip", null), Audit(requesterUserId));

        var cancelResult = await service.CancelAsync(submitResult.Value.Id, Guid.NewGuid(), submitResult.Value.Version, Audit(requesterUserId));

        Assert.True(cancelResult.IsFailure);
        Assert.Equal("leaverequest.not_owner", cancelResult.Error!.Code);
    }
}
