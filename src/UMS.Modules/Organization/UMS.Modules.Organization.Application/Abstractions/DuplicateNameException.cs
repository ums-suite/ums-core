namespace UMS.Modules.Organization.Application.Abstractions;

/// <summary>
/// edge-cases.md "Two Departments with the identical name created simultaneously under the same
/// Faculty": thrown by <c>OrganizationDbContext.SaveChangesAsync</c> when a Postgres composite
/// unique-violation on a `(parent_id, name)` constraint is caught - the DB constraint, not an
/// application-level check-then-insert, is the actual enforcement point (design-decisions.md,
/// "Uniqueness Enforcement Mechanism"). Mirrors Identity's own <c>DuplicateUserException</c>
/// pattern exactly.
/// </summary>
public sealed class DuplicateNameException(string entityType, string name) : Exception(
    $"A {entityType} named '{name}' already exists under this parent.")
{
    public string EntityType { get; } = entityType;

    public string Name { get; } = name;
}
