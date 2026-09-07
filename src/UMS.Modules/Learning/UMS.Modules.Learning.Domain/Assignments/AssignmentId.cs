namespace UMS.Modules.Learning.Domain.Assignments;

public readonly record struct AssignmentId(Guid Value)
{
    public static AssignmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
