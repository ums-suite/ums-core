namespace UMS.Modules.Alumni.Domain.AlumniEvents;

public readonly record struct AlumniEventId(Guid Value)
{
    public static AlumniEventId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
