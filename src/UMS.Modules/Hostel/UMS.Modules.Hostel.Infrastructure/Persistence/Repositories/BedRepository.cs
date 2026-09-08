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

    /// <summary>
    /// Deliberately two plain, uncorrelated queries materialized in C# rather than one combined
    /// EF LINQ query - a `join` against a correlated `Allocations.Any(...)` subquery referencing a
    /// strongly-typed id's converted `.Value` repeatedly failed to translate (EF Core's
    /// HasConversion limitation this repo's other repositories already document for a single
    /// equality comparison; here it also breaks under `Contains` and under a correlated `Any`
    /// nested inside a `join`). Bed/Room/Allocation counts per Hostel are small and this is a
    /// low-frequency, officer-driven path (design-decisions.md), so client-side filtering after
    /// two small materialized lists costs nothing in practice.
    /// </summary>
    public async Task<AvailableBedCandidate?> FindAvailableBedAsync(Guid hostelId, RoomType roomType, IReadOnlyCollection<Guid> excludeBedIds, CancellationToken cancellationToken = default)
    {
        var typedHostelId = new HostelId(hostelId);

        var candidateBeds = await (
            from bed in context.Beds
            join room in context.Rooms on bed.RoomId equals room.Id
            where room.HostelId == typedHostelId && room.Type == roomType
            orderby bed.Id
            select new AvailableBedCandidate(bed.Id.Value, room.Id.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidateBeds.Count == 0)
        {
            return null;
        }

        var candidateBedIds = candidateBeds.Select(c => c.BedId).ToList();
        var occupiedBedIds = await context.Allocations
            .Where(a => candidateBedIds.Contains(a.BedId) && (a.Status == AllocationStatus.Pending || a.Status == AllocationStatus.FeePaid || a.Status == AllocationStatus.Active))
            .Select(a => a.BedId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var unavailableBedIds = new HashSet<Guid>(occupiedBedIds);
        unavailableBedIds.UnionWith(excludeBedIds);

        return candidateBeds.FirstOrDefault(c => !unavailableBedIds.Contains(c.BedId));
    }

    public void Add(Bed bed) => context.Beds.Add(bed);
}
