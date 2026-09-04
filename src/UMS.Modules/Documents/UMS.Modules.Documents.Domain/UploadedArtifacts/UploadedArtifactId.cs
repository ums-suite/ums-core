namespace UMS.Modules.Documents.Domain.UploadedArtifacts;

public readonly record struct UploadedArtifactId(Guid Value)
{
    public static UploadedArtifactId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
