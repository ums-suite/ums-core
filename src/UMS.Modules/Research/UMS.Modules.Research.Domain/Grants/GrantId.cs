namespace UMS.Modules.Research.Domain.Grants;

public readonly record struct GrantId(Guid Value)
{
    public static GrantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
