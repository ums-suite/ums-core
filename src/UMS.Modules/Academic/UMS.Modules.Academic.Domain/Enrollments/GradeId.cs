namespace UMS.Modules.Academic.Domain.Enrollments;

public readonly record struct GradeId(Guid Value)
{
    public static GradeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
