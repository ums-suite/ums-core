namespace UMS.Modules.Academic.Domain.Courses;

public readonly record struct CourseId(Guid Value)
{
    public static CourseId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
