namespace UMS.Modules.Academic.Domain.CourseOfferings;

public readonly record struct CourseOfferingId(Guid Value)
{
    public static CourseOfferingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
