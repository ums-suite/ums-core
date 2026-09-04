using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Facilities;

/// <summary>
/// The atomic physical space unit under a Building (glossary), reused by id from Hostel (bed
/// allocation), Library (branch location), and Academic (exam-room scheduling) - "Organization
/// allows a Room to be referenced by id from more than one consuming module concurrently ... that
/// conflict-avoidance logic belongs to whichever module is doing the booking" (requirement-spec.md
/// organization §8). Room, alongside Building, is the one level of the hierarchy where hard delete
/// is genuinely permitted (design-decisions.md, "Soft-Delete/Deactivate-Only Pattern") - there is
/// deliberately no <see cref="Common.NodeStatus"/> here at all, since deactivation is not this
/// level's lifecycle model.
/// </summary>
public sealed class Room : AggregateRoot<RoomId>
{
    private Room()
    {
    }

    private Room(RoomId id, BuildingId buildingId, string name, int? capacity, string? roomType, DateTimeOffset now)
    {
        Id = id;
        BuildingId = buildingId;
        Name = name;
        Capacity = capacity;
        RoomType = roomType;
        CreatedAt = now;
    }

    public BuildingId BuildingId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int? Capacity { get; private set; }

    /// <summary>Free text (e.g. "classroom", "lab", "exam-hall") - Organization has no opinion on what a Room is used for (organization §8), so this is descriptive metadata only, never an enum a consuming module's scheduling logic should branch on.</summary>
    public string? RoomType { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Room Create(BuildingId buildingId, string name, int? capacity, string? roomType, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Room name/number is required.", nameof(name));
        }

        if (capacity is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Room capacity cannot be negative.");
        }

        return new Room(RoomId.New(), buildingId, name.Trim(), capacity, roomType?.Trim(), now);
    }
}
