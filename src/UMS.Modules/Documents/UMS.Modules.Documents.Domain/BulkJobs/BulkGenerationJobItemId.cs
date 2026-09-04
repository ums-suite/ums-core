namespace UMS.Modules.Documents.Domain.BulkJobs;

public readonly record struct BulkGenerationJobItemId(Guid Value)
{
    public static BulkGenerationJobItemId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
