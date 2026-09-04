namespace UMS.Modules.Student.Domain.Guardians;

public readonly record struct GuardianId(Guid Value)
{
    public static GuardianId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
