using Microsoft.EntityFrameworkCore;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.LeaveRequests;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Repositories;

internal sealed class LeaveRequestRepository(FacultyDbContext context) : ILeaveRequestRepository
{
    public Task<LeaveRequest?> GetByIdAsync(LeaveRequestId id, CancellationToken cancellationToken = default) =>
        context.LeaveRequests.Include(l => l.ReasonTranslations).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<LeaveRequest>> ListByFacultyMemberAsync(Guid facultyMemberId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.LeaveRequests
            .Include(l => l.ReasonTranslations)
            .Where(l => l.FacultyMemberId == facultyMemberId)
            .OrderByDescending(l => l.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(LeaveRequest leaveRequest) => context.LeaveRequests.Add(leaveRequest);
}
