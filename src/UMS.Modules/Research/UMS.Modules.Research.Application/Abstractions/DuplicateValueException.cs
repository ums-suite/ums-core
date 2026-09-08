namespace UMS.Modules.Research.Application.Abstractions;

/// <summary>Translated from a Postgres unique-violation by the DbContext's own <c>SaveChangesAsync</c> override - e.g. Publication's DOI uniqueness invariant (requirement-spec.md §4). Mirrors every other module's own exception exactly.</summary>
public sealed class DuplicateValueException(string entityType, string fieldName, string value)
    : Exception($"A(n) '{entityType}' with {fieldName} '{value}' already exists.")
{
    public string EntityType { get; } = entityType;

    public string FieldName { get; } = fieldName;
}
