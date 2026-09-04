namespace UMS.Modules.Documents.Domain.BulkJobs;

public readonly record struct BulkGenerationJobId(Guid Value)
{
    public static BulkGenerationJobId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
