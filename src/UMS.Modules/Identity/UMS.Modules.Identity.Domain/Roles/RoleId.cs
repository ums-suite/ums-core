namespace UMS.Modules.Identity.Domain.Roles;

public readonly record struct RoleId(Guid Value)
{
    public static RoleId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
