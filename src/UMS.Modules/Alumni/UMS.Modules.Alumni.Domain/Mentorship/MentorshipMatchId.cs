namespace UMS.Modules.Alumni.Domain.Mentorship;

public readonly record struct MentorshipMatchId(Guid Value)
{
    public static MentorshipMatchId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
