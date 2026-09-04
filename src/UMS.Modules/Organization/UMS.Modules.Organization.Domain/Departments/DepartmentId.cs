namespace UMS.Modules.Organization.Domain.Departments;

public readonly record struct DepartmentId(Guid Value)
{
    public static DepartmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
