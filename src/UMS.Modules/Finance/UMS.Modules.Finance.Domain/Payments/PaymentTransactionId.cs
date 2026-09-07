namespace UMS.Modules.Finance.Domain.Payments;

public readonly record struct PaymentTransactionId(Guid Value)
{
    public static PaymentTransactionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
