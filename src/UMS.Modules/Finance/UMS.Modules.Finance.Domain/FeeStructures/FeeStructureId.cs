namespace UMS.Modules.Finance.Domain.FeeStructures;

public readonly record struct FeeStructureId(Guid Value)
{
    public static FeeStructureId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
