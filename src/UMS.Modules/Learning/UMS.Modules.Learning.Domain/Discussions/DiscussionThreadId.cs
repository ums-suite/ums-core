namespace UMS.Modules.Learning.Domain.Discussions;

public readonly record struct DiscussionThreadId(Guid Value)
{
    public static DiscussionThreadId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
