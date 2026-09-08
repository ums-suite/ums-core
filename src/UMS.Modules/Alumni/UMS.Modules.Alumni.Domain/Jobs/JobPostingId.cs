namespace UMS.Modules.Alumni.Domain.Jobs;

public readonly record struct JobPostingId(Guid Value)
{
    public static JobPostingId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
