namespace UMS.Modules.Career.Domain.Internships;

public readonly record struct InternshipId(Guid Value)
{
    public static InternshipId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
