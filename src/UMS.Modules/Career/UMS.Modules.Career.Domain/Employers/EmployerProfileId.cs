namespace UMS.Modules.Career.Domain.Employers;

public readonly record struct EmployerProfileId(Guid Value)
{
    public static EmployerProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
