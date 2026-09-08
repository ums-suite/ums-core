using UMS.Modules.Hostel.Domain.ApplicationWindows;

namespace UMS.Modules.Hostel.Application.Abstractions;

public interface IApplicationWindowRepository
{
    public Task<ApplicationWindow?> GetByIdAsync(ApplicationWindowId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<ApplicationWindow>> GetAllAsync(CancellationToken cancellationToken = default);

    public void Add(ApplicationWindow window);
}
