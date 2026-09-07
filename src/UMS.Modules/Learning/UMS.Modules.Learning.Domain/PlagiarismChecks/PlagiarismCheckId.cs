namespace UMS.Modules.Learning.Domain.PlagiarismChecks;

public readonly record struct PlagiarismCheckId(Guid Value)
{
    public static PlagiarismCheckId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
