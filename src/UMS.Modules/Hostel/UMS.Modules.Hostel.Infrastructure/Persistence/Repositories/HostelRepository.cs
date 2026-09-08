using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class HostelRepository(HostelDbContext context) : IHostelRepository
{
    public Task<Domain.Hostels.Hostel?> GetByIdAsync(HostelId id, CancellationToken cancellationToken = default) =>
        context.Hostels.FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Domain.Hostels.Hostel>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await context.Hostels.OrderBy(h => h.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Domain.Hostels.Hostel hostel) => context.Hostels.Add(hostel);
}
