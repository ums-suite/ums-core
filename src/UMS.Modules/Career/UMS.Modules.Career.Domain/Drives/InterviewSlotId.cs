namespace UMS.Modules.Career.Domain.Drives;

public readonly record struct InterviewSlotId(Guid Value)
{
    public static InterviewSlotId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
