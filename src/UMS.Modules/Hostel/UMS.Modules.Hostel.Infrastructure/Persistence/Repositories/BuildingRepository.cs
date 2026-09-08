using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class BuildingRepository(HostelDbContext context) : IBuildingRepository
{
    public Task<Building?> GetByIdAsync(BuildingId id, CancellationToken cancellationToken = default) =>
        context.Buildings.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Building>> GetByHostelAsync(HostelId hostelId, CancellationToken cancellationToken = default) =>
        await context.Buildings.Where(b => b.HostelId == hostelId).OrderBy(b => b.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Building building) => context.Buildings.Add(building);
}
