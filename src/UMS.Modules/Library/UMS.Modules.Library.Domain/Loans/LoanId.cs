namespace UMS.Modules.Library.Domain.Loans;

public readonly record struct LoanId(Guid Value)
{
    public static LoanId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
