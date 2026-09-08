namespace UMS.Modules.Hostel.Domain.ApplicationWindows;

public readonly record struct ApplicationWindowId(Guid Value)
{
    public static ApplicationWindowId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
