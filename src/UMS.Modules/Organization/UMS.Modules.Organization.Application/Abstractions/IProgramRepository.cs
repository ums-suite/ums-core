using UMS.Modules.Organization.Domain.Departments;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;
using OrgProgramId = UMS.Modules.Organization.Domain.Programs.ProgramId;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface IProgramRepository
{
    public Task<OrgProgram?> GetByIdAsync(OrgProgramId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<OrgProgram>> ListAsync(DepartmentId? departmentId, int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(DepartmentId? departmentId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<OrgProgram>> ListAllAsync(CancellationToken cancellationToken = default);

    public void Add(OrgProgram program);
}
