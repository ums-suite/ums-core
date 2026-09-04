using UMS.Modules.Faculty.Domain.FacultyMembers;

namespace UMS.Modules.Faculty.Application.Abstractions;

public interface IFacultyMemberRepository
{
    public Task<FacultyMember?> GetByIdAsync(FacultyMemberId id, CancellationToken cancellationToken = default);

    public Task<FacultyMember?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    public Task<bool> HasAnyActiveInDepartmentAsync(Guid departmentId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<FacultyMember>> ListAsync(Guid? departmentId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(Guid? departmentId, CancellationToken cancellationToken = default);

    public void Add(FacultyMember facultyMember);
}
