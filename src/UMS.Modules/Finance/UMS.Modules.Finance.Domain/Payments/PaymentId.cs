namespace UMS.Modules.Finance.Domain.Payments;

public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
