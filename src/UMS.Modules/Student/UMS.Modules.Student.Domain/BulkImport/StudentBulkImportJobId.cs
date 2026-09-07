namespace UMS.Modules.Student.Domain.BulkImport;

public readonly record struct StudentBulkImportJobId(Guid Value)
{
    public static StudentBulkImportJobId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
