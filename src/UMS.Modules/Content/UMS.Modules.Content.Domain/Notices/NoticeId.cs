namespace UMS.Modules.Content.Domain.Notices;

public readonly record struct NoticeId(Guid Value)
{
    public static NoticeId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
