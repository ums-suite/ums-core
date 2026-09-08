namespace UMS.Modules.Research.Domain.FundingBodies;

public readonly record struct FundingBodyId(Guid Value)
{
    public static FundingBodyId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
