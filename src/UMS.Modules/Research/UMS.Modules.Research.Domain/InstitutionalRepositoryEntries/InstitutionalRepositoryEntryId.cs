namespace UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;

public readonly record struct InstitutionalRepositoryEntryId(Guid Value)
{
    public static InstitutionalRepositoryEntryId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
