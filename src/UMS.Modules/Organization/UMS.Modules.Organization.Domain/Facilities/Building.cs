using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Facilities;

/// <summary>
/// A physical academic/administrative space, referenced by id from Hostel/Library/Academic
/// (glossary: "Organization owns the physical-space record; the referencing module owns what that
/// space is used for"). Sited on a Campus - not stated as a required parent in requirement-spec.md
/// §6's API surface, but a physical Building always sits somewhere, and this module's own hard-
/// delete decision (design-decisions.md, "Soft-Delete/Deactivate-Only Pattern") already
/// distinguishes Building/Room from the University→Campus→Faculty→Department→Program chain, so
/// requiring a Campus link here is a structural completeness call, not a re-litigation of that
/// chain. No `PATCH`/deactivate endpoint exists (tickets.md's Flagged Gaps) - a Building only
/// creates, lists, and (design-decisions.md, "hard delete retained only for Room/Building")
/// hard-deletes.
/// </summary>
public sealed class Building : AggregateRoot<BuildingId>
{
    private Building()
    {
    }

    private Building(BuildingId id, CampusId campusId, string name, string? code, DateTimeOffset now)
    {
        Id = id;
        CampusId = campusId;
        Name = name;
        Code = code;
        CreatedAt = now;
    }

    public CampusId CampusId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Code { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Building Create(CampusId campusId, string name, string? code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Building name is required.", nameof(name));
        }

        return new Building(BuildingId.New(), campusId, name.Trim(), code?.Trim(), now);
    }
}
