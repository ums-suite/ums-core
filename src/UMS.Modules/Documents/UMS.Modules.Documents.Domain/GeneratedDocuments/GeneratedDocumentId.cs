namespace UMS.Modules.Documents.Domain.GeneratedDocuments;

/// <summary>Documents' own strongly-typed id (ums-conventions.md, Domain Modeling) for the metadata-only <see cref="GeneratedDocument"/> aggregate (ADR-0010).</summary>
public readonly record struct GeneratedDocumentId(Guid Value)
{
    public static GeneratedDocumentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
