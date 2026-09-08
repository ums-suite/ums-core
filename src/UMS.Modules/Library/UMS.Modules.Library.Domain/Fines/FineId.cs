namespace UMS.Modules.Library.Domain.Fines;

public readonly record struct FineId(Guid Value)
{
    public static FineId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
