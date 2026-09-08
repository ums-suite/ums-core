namespace UMS.Modules.Content.Domain.Banners;

public readonly record struct BannerId(Guid Value)
{
    public static BannerId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
