namespace UMS.Modules.Student.Domain.StudentRequests;

public readonly record struct StudentRequestId(Guid Value)
{
    public static StudentRequestId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
