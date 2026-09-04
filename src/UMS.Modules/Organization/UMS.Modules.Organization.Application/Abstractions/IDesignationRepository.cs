using UMS.Modules.Organization.Domain.Designations;

namespace UMS.Modules.Organization.Application.Abstractions;

public interface IDesignationRepository
{
    public Task<Designation?> GetByIdAsync(DesignationId id, CancellationToken cancellationToken = default);

    public Task<bool> ExistsByTitleAsync(string title, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Designation>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);

    public Task<int> CountAsync(CancellationToken cancellationToken = default);

    public void Add(Designation designation);
}
