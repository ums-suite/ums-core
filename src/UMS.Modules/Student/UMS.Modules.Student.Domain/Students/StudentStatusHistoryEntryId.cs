namespace UMS.Modules.Student.Domain.Students;

public readonly record struct StudentStatusHistoryEntryId(Guid Value)
{
    public static StudentStatusHistoryEntryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
