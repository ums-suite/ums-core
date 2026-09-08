namespace UMS.Modules.Content.Domain.Downloads;

public readonly record struct DownloadResourceId(Guid Value)
{
    public static DownloadResourceId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
