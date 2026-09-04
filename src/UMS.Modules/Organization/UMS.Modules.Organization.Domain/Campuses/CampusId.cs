namespace UMS.Modules.Organization.Domain.Campuses;

public readonly record struct CampusId(Guid Value)
{
    public static CampusId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
