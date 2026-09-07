namespace UMS.Modules.Academic.Domain.Curricula;

public readonly record struct CurriculumId(Guid Value)
{
    public static CurriculumId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
