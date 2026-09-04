namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>Thrown when a Postgres unique-violation is caught on a Student-owned constraint (e.g. <c>originating_application_id</c> or the full <c>student_number</c> string). Mirrors Faculty's own <c>DuplicateValueException</c> pattern.</summary>
public sealed class DuplicateValueException(string entityType, string field, string value) : Exception(
    $"A {entityType} with {field} '{value}' already exists.")
{
    public string EntityType { get; } = entityType;

    public string Field { get; } = field;
}
