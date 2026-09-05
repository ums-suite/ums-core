namespace UMS.Modules.Academic.Domain.Programs;

public readonly record struct ProgramId(Guid Value)
{
    public static ProgramId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
