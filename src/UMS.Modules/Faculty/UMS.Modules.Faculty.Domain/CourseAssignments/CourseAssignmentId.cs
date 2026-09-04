namespace UMS.Modules.Faculty.Domain.CourseAssignments;

public readonly record struct CourseAssignmentId(Guid Value)
{
    public static CourseAssignmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
