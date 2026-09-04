namespace UMS.Modules.Documents.Domain.Templates;

/// <summary>Documents' own strongly-typed id (ums-conventions.md, Domain Modeling) - never a bare <see cref="Guid"/> crossing <see cref="DocumentTemplate"/>'s public boundary.</summary>
public readonly record struct DocumentTemplateId(Guid Value)
{
    public static DocumentTemplateId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
