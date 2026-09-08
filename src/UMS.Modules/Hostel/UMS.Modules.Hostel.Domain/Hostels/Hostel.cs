using UMS.Modules.Hostel.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Hostels;

/// <summary>
/// HOS-1: a residential facility (docs/ddd/ubiquitous-language.md: "A residential facility
/// containing Buildings, Rooms, and Beds"). requirement-spec.md §9 decision 1: Hostel owns its own
/// <see cref="Building"/>/<see cref="Room"/> as child entities of this aggregate, structurally
/// distinct from Organization's academic Building/Room - deliberately no Organization dependency.
///
/// <para>
/// <see cref="Building"/>, <see cref="Room"/>, and <see cref="Bed"/> are modeled as their own
/// top-level entities (own table, own strongly-typed id, own repository) rather than nested
/// <c>OwnsMany</c> owned collections under this aggregate - mirroring Organization's own
/// <c>Building</c>/<c>Campus</c> precedent. This is a deliberate, practical choice: the
/// bed-allocation concurrency mechanism (design-decisions.md) requires taking a
/// <c>SELECT ... FOR UPDATE</c> lock directly on one target <see cref="Bed"/> row (and, separately,
/// on one target <see cref="Room"/> row for the capacity-reduction/bed-provisioning checks) -
/// something a three-level-deep owned-collection shape would make materially harder to reason about
/// and would additionally multiply the EF Core 10 owned-collection insert-vs-update shadow-key
/// gotcha across three nesting levels for no correctness benefit. The consistency boundary these
/// entities share is still rooted at this aggregate (every <see cref="Building"/> carries a
/// <see cref="HostelId"/>, every <see cref="Room"/> a <see cref="BuildingId"/>, every
/// <see cref="Bed"/> a <see cref="RoomId"/>) - only the EF persistence shape differs from a literal
/// nested-owned-type mapping.
/// </para>
/// </summary>
public sealed class Hostel : AggregateRoot<HostelId>
{
    private Hostel()
    {
    }

    private Hostel(HostelId id, string name, HostelType type, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Type = type;
        CreatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public HostelType Type { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Hostel> Create(string name, HostelType type, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("hostel.name_required", "A Hostel's name is required.");
        }

        return new Hostel(HostelId.New(), name.Trim(), type, now);
    }

    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("hostel.name_required", "A Hostel's name is required."));
        }

        Name = name.Trim();
        return Result.Success();
    }

    public void ChangeType(HostelType type) => Type = type;
}
