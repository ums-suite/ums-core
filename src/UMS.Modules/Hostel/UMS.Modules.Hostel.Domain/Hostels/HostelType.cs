namespace UMS.Modules.Hostel.Domain.Hostels;

/// <summary>requirement-spec.md §2: "name, type - e.g. male/female/international, capacity summary".</summary>
public enum HostelType
{
    Male,
    Female,
    International,
    Mixed,
}
