namespace UMS.Modules.Academic.Domain.CourseOfferings;

public readonly record struct AssessmentId(Guid Value)
{
    public static AssessmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
