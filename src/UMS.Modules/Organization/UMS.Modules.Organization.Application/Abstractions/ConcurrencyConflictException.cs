namespace UMS.Modules.Organization.Application.Abstractions;

/// <summary>
/// edge-cases.md "Concurrent edits to the same Department/Program (lost update)": thrown by
/// <c>OrganizationDbContext.SaveChangesAsync</c> when EF Core's own
/// <c>DbUpdateConcurrencyException</c> fires - i.e. the `xmin`-backed version a `PATCH` submitted
/// no longer matches the row's current version (design-decisions.md, "Optimistic Concurrency").
/// Translating it into this plain, Infrastructure-framework-free exception here (rather than
/// letting <c>DbUpdateConcurrencyException</c> itself leak upward) keeps the Application layer
/// from needing an EF Core package reference at all, matching Identity's own layering.
/// </summary>
public sealed class ConcurrencyConflictException(string entityType) : Exception(
    $"The {entityType} was modified by someone else since it was last read - refetch and retry.")
{
    public string EntityType { get; } = entityType;
}
