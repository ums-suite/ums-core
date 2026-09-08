namespace UMS.Modules.Career.Domain.ResumeProfiles;

public readonly record struct ResumeProfileId(Guid Value)
{
    public static ResumeProfileId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
