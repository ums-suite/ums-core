namespace UMS.Modules.Hostel.Domain.Applications;

public readonly record struct HostelApplicationId(Guid Value)
{
    public static HostelApplicationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
