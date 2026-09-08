namespace UMS.Modules.Hostel.Domain.Hostels;

public readonly record struct BedId(Guid Value)
{
    public static BedId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
