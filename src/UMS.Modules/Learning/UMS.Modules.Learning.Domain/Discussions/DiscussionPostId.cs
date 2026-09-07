namespace UMS.Modules.Learning.Domain.Discussions;

public readonly record struct DiscussionPostId(Guid Value)
{
    public static DiscussionPostId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
