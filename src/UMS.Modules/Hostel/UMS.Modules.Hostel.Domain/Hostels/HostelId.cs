namespace UMS.Modules.Hostel.Domain.Hostels;

public readonly record struct HostelId(Guid Value)
{
    public static HostelId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
