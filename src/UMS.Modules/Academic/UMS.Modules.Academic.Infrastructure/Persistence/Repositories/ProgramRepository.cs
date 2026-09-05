using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.Programs;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class ProgramRepository(AcademicDbContext context) : IProgramRepository
{
    public Task<Program?> GetByIdAsync(ProgramId id, CancellationToken cancellationToken = default) =>
        context.Programs.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(Program program) => context.Programs.Add(program);
}
