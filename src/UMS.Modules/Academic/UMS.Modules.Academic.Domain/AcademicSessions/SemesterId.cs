namespace UMS.Modules.Academic.Domain.AcademicSessions;

public readonly record struct SemesterId(Guid Value)
{
    public static SemesterId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
