using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Facilities;

namespace UMS.Modules.Organization.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(OrganizationDbContext context) : IRoomRepository
{
    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken cancellationToken = default) =>
        context.Rooms.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<bool> HasAnyUnderAsync(BuildingId buildingId, CancellationToken cancellationToken = default) =>
        context.Rooms.AnyAsync(r => r.BuildingId == buildingId, cancellationToken);

    public async Task<IReadOnlyList<Room>> ListByBuildingAsync(BuildingId buildingId, int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Rooms
            .Where(r => r.BuildingId == buildingId)
            .OrderBy(r => r.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<int> CountByBuildingAsync(BuildingId buildingId, CancellationToken cancellationToken = default) =>
        context.Rooms.CountAsync(r => r.BuildingId == buildingId, cancellationToken);

    public void Add(Room room) => context.Rooms.Add(room);

    public void Remove(Room room) => context.Rooms.Remove(room);
}
