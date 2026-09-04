namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>Mirrors Faculty's own <c>ConcurrencyConflictException</c> exactly - thrown when an `xmin`-backed optimistic-concurrency check fails (design-decisions.md, "Status-Change Transactional Boundary").</summary>
public sealed class ConcurrencyConflictException(string entityType) : Exception(
    $"The {entityType} was modified by someone else since it was last read - refetch and retry.")
{
    public string EntityType { get; } = entityType;
}
