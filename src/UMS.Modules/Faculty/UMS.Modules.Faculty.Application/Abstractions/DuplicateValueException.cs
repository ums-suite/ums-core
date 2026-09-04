namespace UMS.Modules.Faculty.Application.Abstractions;

/// <summary>Thrown when a Postgres unique-violation is caught on a Faculty-owned constraint (e.g. one active FacultyMember per EmployeeId). Mirrors Organization's own <c>DuplicateNameException</c> pattern.</summary>
public sealed class DuplicateValueException(string entityType, string field, string value) : Exception(
    $"A {entityType} with {field} '{value}' already exists.")
{
    public string EntityType { get; } = entityType;
}
