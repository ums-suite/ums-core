namespace UMS.Modules.Content.Domain.HomepageSections;

public readonly record struct HomepageSectionId(Guid Value)
{
    public static HomepageSectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
