namespace UMS.Modules.Finance.Domain.Reconciliation;

public readonly record struct ReconciliationExceptionId(Guid Value)
{
    public static ReconciliationExceptionId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
