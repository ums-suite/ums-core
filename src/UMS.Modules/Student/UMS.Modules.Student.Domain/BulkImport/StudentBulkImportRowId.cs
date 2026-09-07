namespace UMS.Modules.Student.Domain.BulkImport;

public readonly record struct StudentBulkImportRowId(Guid Value)
{
    public static StudentBulkImportRowId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
