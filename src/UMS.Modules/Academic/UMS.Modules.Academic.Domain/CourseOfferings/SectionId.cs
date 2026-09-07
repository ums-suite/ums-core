namespace UMS.Modules.Academic.Domain.CourseOfferings;

public readonly record struct SectionId(Guid Value)
{
    public static SectionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
