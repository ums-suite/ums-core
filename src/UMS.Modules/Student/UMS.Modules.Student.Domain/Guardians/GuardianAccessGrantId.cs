namespace UMS.Modules.Student.Domain.Guardians;

public readonly record struct GuardianAccessGrantId(Guid Value)
{
    public static GuardianAccessGrantId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
