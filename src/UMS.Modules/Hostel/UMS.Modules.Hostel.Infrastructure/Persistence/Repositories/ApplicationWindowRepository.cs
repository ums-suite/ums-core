using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.ApplicationWindows;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class ApplicationWindowRepository(HostelDbContext context) : IApplicationWindowRepository
{
    public Task<ApplicationWindow?> GetByIdAsync(ApplicationWindowId id, CancellationToken cancellationToken = default) =>
        context.ApplicationWindows.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ApplicationWindow>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.ApplicationWindows.OrderByDescending(w => w.OpensAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(ApplicationWindow window) => context.ApplicationWindows.Add(window);
}
