namespace UMS.Modules.Library.Application.Abstractions;

/// <summary>Thrown by the DbContext when a unique-constraint violation is translated at the Infrastructure layer. Mirrors every other module's own copy exactly.</summary>
public sealed class DuplicateValueException(string entityType, string field, string value) : Exception(
    $"A {entityType} with {field} '{value}' already exists.")
{
    public string EntityType { get; } = entityType;

    public string Field { get; } = field;

    public string Value { get; } = value;
}
