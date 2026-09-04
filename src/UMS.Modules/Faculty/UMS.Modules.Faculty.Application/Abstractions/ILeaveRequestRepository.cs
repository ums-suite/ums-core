using UMS.Modules.Faculty.Domain.LeaveRequests;

namespace UMS.Modules.Faculty.Application.Abstractions;

public interface ILeaveRequestRepository
{
    public Task<LeaveRequest?> GetByIdAsync(LeaveRequestId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<LeaveRequest>> ListByFacultyMemberAsync(Guid facultyMemberId, int skip, int take, CancellationToken cancellationToken = default);

    public void Add(LeaveRequest leaveRequest);
}
