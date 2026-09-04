namespace UMS.Modules.Organization.Domain.Designations;

public readonly record struct DesignationId(Guid Value)
{
    public static DesignationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
