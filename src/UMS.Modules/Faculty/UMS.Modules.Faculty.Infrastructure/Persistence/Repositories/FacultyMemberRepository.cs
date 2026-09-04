using Microsoft.EntityFrameworkCore;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.FacultyMembers;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Repositories;

internal sealed class FacultyMemberRepository(FacultyDbContext context) : IFacultyMemberRepository
{
    public Task<FacultyMember?> GetByIdAsync(FacultyMemberId id, CancellationToken cancellationToken = default) =>
        context.FacultyMembers.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<FacultyMember?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        context.FacultyMembers.FirstOrDefaultAsync(f => f.UserId == userId, cancellationToken);

    public Task<bool> HasAnyActiveInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default) =>
        context.FacultyMembers.AnyAsync(f => f.DepartmentId == departmentId && f.Status == FacultyMemberStatus.Active, cancellationToken);

    public async Task<IReadOnlyList<FacultyMember>> ListAsync(Guid? departmentId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.FacultyMembers
            .Where(f => departmentId == null || f.DepartmentId == departmentId.Value)
            .OrderBy(f => f.EmployeeId)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(Guid? departmentId, CancellationToken cancellationToken = default) =>
        context.FacultyMembers.CountAsync(f => departmentId == null || f.DepartmentId == departmentId.Value, cancellationToken);

    public void Add(FacultyMember facultyMember) => context.FacultyMembers.Add(facultyMember);
}
