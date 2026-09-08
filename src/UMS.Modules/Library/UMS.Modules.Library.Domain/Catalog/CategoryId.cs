namespace UMS.Modules.Library.Domain.Catalog;

public readonly record struct CategoryId(Guid Value)
{
    public static CategoryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
