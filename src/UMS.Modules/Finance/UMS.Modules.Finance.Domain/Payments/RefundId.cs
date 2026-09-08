namespace UMS.Modules.Finance.Domain.Payments;

public readonly record struct RefundId(Guid Value)
{
    public static RefundId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
