namespace UMS.Modules.Organization.Domain.Universities;

public readonly record struct UniversityId(Guid Value)
{
    public static UniversityId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
