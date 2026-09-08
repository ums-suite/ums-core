using UMS.Modules.Hostel.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Hostels;

/// <summary>
/// HOS-1: docs/ddd/ubiquitous-language.md - "the atomic allocatable unit within a Room; exactly one
/// active Allocation per Bed." This entity itself carries no occupancy state - availability is
/// derived entirely from <c>hostel.allocations</c> (whether an <c>Active</c>-status Allocation
/// references this Bed), never a denormalized status field here, so there is exactly one place
/// (the Allocation's own partial-unique-index-backed state) that can ever disagree with itself.
/// design-decisions.md "Bed-Allocation Concurrency Control Pattern": this Bed row is still the
/// target of the <c>SELECT ... FOR UPDATE</c> lock every allocation-creating AND allocation-freeing
/// (check-out) writer takes, exactly mirroring Finance's own <c>Payment</c>/<c>Invoice</c> lock
/// pattern where the locked row and the row actually being written can be two different tables.
/// </summary>
public sealed class Bed : AggregateRoot<BedId>
{
    private Bed()
    {
    }

    private Bed(BedId id, RoomId roomId, string label, DateTimeOffset now)
    {
        Id = id;
        RoomId = roomId;
        Label = label;
        CreatedAt = now;
    }

    public RoomId RoomId { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Bed> Create(RoomId roomId, string label, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return Error.Validation("bed.label_required", "A Bed's label is required.");
        }

        return new Bed(BedId.New(), roomId, label.Trim(), now);
    }
}
