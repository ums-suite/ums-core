namespace UMS.Modules.Academic.Domain.Enrollments;

public readonly record struct EnrollmentId(Guid Value)
{
    public static EnrollmentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
