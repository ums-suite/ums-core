namespace UMS.Modules.Academic.Domain.AcademicSessions;

public readonly record struct AcademicSessionId(Guid Value)
{
    public static AcademicSessionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
