namespace UMS.Modules.Alumni.Application.Abstractions;

public sealed class DuplicateValueException(string entityType, string fieldName, string value)
    : Exception($"A '{entityType}' with {fieldName} '{value}' already exists.")
{
    public string EntityType { get; } = entityType;

    public string FieldName { get; } = fieldName;
}
