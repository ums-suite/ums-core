using UMS.Modules.Hostel.Domain.Hostels;

namespace UMS.Modules.Hostel.Application.Abstractions;

public interface IBedRepository
{
    public Task<Bed?> GetByIdAsync(BedId id, CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Bed-Allocation Concurrency Control Pattern": the pessimistic lock every allocation-creating AND allocation-freeing (check-out) writer takes before mutating occupancy state.</summary>
    public Task<Bed?> GetByIdForUpdateAsync(BedId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Bed>> GetByRoomAsync(RoomId roomId, CancellationToken cancellationToken = default);

    public Task<int> CountByRoomAsync(RoomId roomId, CancellationToken cancellationToken = default);

    /// <summary>
    /// HOS-7/HOS-14: an unlocked candidate search - a Bed in the given Hostel/RoomType with no
    /// Active/Pending Allocation referencing it, excluding <paramref name="excludeBedIds"/> (already
    /// tried and lost a race against). The caller MUST re-verify under
    /// <see cref="GetByIdForUpdateAsync"/> before trusting this result - a plain unlocked read is
    /// only ever a candidate hint, never itself the serialization point (design-decisions.md
    /// "Bed-Allocation Concurrency Control Pattern") - see <c>AllocationService</c>'s own remarks.
    /// </summary>
    public Task<AvailableBedCandidate?> FindAvailableBedAsync(Guid hostelId, RoomType roomType, IReadOnlyCollection<Guid> excludeBedIds, CancellationToken cancellationToken = default);

    public void Add(Bed bed);
}

public sealed record AvailableBedCandidate(Guid BedId, Guid RoomId);
