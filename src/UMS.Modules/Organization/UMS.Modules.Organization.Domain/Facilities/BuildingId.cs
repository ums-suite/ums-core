namespace UMS.Modules.Organization.Domain.Facilities;

public readonly record struct BuildingId(Guid Value)
{
    public static BuildingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
