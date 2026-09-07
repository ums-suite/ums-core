using UMS.Modules.Academic.Domain.Programs;

namespace UMS.Modules.Academic.Application.Abstractions;

public interface IProgramRepository
{
    public Task<Program?> GetByIdAsync(ProgramId id, CancellationToken cancellationToken = default);

    public void Add(Program program);
}
