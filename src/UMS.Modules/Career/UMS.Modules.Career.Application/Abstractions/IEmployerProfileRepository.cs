using UMS.Modules.Career.Domain.Employers;

namespace UMS.Modules.Career.Application.Abstractions;

public interface IEmployerProfileRepository
{
    public Task<EmployerProfile?> GetByIdAsync(EmployerProfileId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<EmployerProfile>> ListAsync(bool includeArchived, int skip, int take, CancellationToken cancellationToken = default);

    public void Add(EmployerProfile profile);
}
