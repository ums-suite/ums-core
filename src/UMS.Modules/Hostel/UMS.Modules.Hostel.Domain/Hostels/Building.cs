using UMS.Modules.Hostel.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Domain.Hostels;

/// <summary>HOS-1: a physical building within a <see cref="Hostel"/> (requirement-spec.md §2 Inventory Management). See <see cref="Hostel"/>'s own remarks on why this is its own top-level entity rather than an owned collection.</summary>
public sealed class Building : AggregateRoot<BuildingId>
{
    private Building()
    {
    }

    private Building(BuildingId id, HostelId hostelId, string name, DateTimeOffset now)
    {
        Id = id;
        HostelId = hostelId;
        Name = name;
        CreatedAt = now;
    }

    public HostelId HostelId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Building> Create(HostelId hostelId, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("building.name_required", "A Building's name is required.");
        }

        return new Building(BuildingId.New(), hostelId, name.Trim(), now);
    }

    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("building.name_required", "A Building's name is required."));
        }

        Name = name.Trim();
        return Result.Success();
    }
}
