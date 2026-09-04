namespace UMS.Modules.Faculty.Domain.ResearchProfiles;

public readonly record struct ResearchProfileId(Guid Value)
{
    public static ResearchProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
