namespace UMS.Modules.Alumni.Domain.Alumni;

public readonly record struct AlumnusId(Guid Value)
{
    public static AlumnusId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
