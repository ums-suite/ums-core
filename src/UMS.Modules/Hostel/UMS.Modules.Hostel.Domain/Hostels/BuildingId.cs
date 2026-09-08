namespace UMS.Modules.Hostel.Domain.Hostels;

public readonly record struct BuildingId(Guid Value)
{
    public static BuildingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
