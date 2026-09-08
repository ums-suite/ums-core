using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class RoomRepository(HostelDbContext context) : IRoomRepository
{
    public Task<Room?> GetByIdAsync(RoomId id, CancellationToken cancellationToken = default) =>
        context.Rooms.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    /// <summary>design-decisions.md "Room-Capacity-Reduction Enforcement Point": no ComplexProperty/needed Include on Room, so the single-step <c>FromSqlInterpolated ... FOR UPDATE</c> form suffices (contrast <see cref="HostelApplicationRepository.GetByIdForUpdateAsync"/>'s two-step form).</summary>
    public Task<Room?> GetByIdForUpdateAsync(RoomId id, CancellationToken cancellationToken = default) =>
        context.Rooms
            .FromSqlInterpolated($"SELECT *, xmin FROM hostel.rooms WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Room>> GetByBuildingAsync(BuildingId buildingId, CancellationToken cancellationToken = default) =>
        await context.Rooms.Where(r => r.BuildingId == buildingId).OrderBy(r => r.RoomNumber).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Room>> GetByHostelAndTypeAsync(HostelId hostelId, RoomType type, CancellationToken cancellationToken = default) =>
        await context.Rooms.Where(r => r.HostelId == hostelId && r.Type == type).OrderBy(r => r.RoomNumber).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(Room room) => context.Rooms.Add(room);
}
