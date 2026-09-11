namespace UMS.Modules.Career.Domain.Applications;

public readonly record struct CareerApplicationId(Guid Value)
{
    public static CareerApplicationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
