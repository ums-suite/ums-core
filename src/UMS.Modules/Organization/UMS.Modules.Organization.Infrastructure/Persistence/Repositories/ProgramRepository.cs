using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Departments;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;
using OrgProgramId = UMS.Modules.Organization.Domain.Programs.ProgramId;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class ProgramRepository(OrganizationDbContext context) : IProgramRepository
{
    public Task<OrgProgram?> GetByIdAsync(OrgProgramId id, CancellationToken cancellationToken = default) =>
        context.Programs.Include(p => p.Translations).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OrgProgram>> ListAsync(DepartmentId? departmentId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Programs
            .Include(p => p.Translations)
            .Where(p => departmentId == null || p.DepartmentId == departmentId.Value)
            .OrderBy(p => p.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountAsync(DepartmentId? departmentId, CancellationToken cancellationToken = default) =>
        context.Programs.CountAsync(p => departmentId == null || p.DepartmentId == departmentId.Value, cancellationToken);

    public async Task<IReadOnlyList<OrgProgram>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await context.Programs.Include(p => p.Translations).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(OrgProgram program) => context.Programs.Add(program);
}
