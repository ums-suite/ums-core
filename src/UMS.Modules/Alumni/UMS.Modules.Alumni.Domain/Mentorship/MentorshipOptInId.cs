namespace UMS.Modules.Alumni.Domain.Mentorship;

public readonly record struct MentorshipOptInId(Guid Value)
{
    public static MentorshipOptInId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
