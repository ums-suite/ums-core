using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class BedRepository(HostelDbContext context) : IBedRepository
{
    public Task<Bed?> GetByIdAsync(BedId id, CancellationToken cancellationToken = default) =>
        context.Beds.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <summary>design-decisions.md "Bed-Allocation Concurrency Control Pattern": no ComplexProperty/needed Include on Bed, so the single-step <c>FromSqlInterpolated ... FOR UPDATE</c> form suffices.</summary>
    public Task<Bed?> GetByIdForUpdateAsync(BedId id, CancellationToken cancellationToken = default) =>
        context.Beds
            .FromSqlInterpolated($"SELECT *, xmin FROM hostel.beds WHERE id = {id.Value} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Bed>> GetByRoomAsync(RoomId roomId, CancellationToken cancellationToken = default) =>
        await context.Beds.Where(b => b.RoomId == roomId).OrderBy(b => b.Label).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<int> CountByRoomAsync(RoomId roomId, CancellationToken cancellationToken = default) =>
        context.Beds.CountAsync(b => b.RoomId == roomId, cancellationToken);

    public async Task<AvailableBedCandidate?> FindAvailableBedAsync(Guid hostelId, RoomType roomType, IReadOnlyCollection<Guid> excludeBedIds, CancellationToken cancellationToken = default)
    {
        var typedHostelId = new HostelId(hostelId);

        return await (
            from bed in context.Beds
            join room in context.Rooms on bed.RoomId equals room.Id
            where room.HostelId == typedHostelId
                  && room.Type == roomType
                  && !excludeBedIds.Contains(bed.Id.Value)
                  && !context.Allocations.Any(a => a.BedId == bed.Id.Value
                      && (a.Status == AllocationStatus.Pending || a.Status == AllocationStatus.FeePaid || a.Status == AllocationStatus.Active))
            orderby bed.Id.Value
            select new AvailableBedCandidate(bed.Id.Value, room.Id.Value))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public void Add(Bed bed) => context.Beds.Add(bed);
}
