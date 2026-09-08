namespace UMS.Modules.Alumni.Domain.Jobs;

public readonly record struct JobApplicationId(Guid Value)
{
    public static JobApplicationId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
