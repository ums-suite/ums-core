namespace UMS.Modules.Academic.Domain.CourseOfferings;

public readonly record struct ExamId(Guid Value)
{
    public static ExamId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
