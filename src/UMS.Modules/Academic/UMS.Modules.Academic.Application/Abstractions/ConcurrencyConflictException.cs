namespace UMS.Modules.Academic.Application.Abstractions;

/// <summary>Thrown by the DbContext when an `xmin`-guarded ordinary write loses an optimistic-concurrency race. Mirrors Faculty/Student's own copy exactly.</summary>
public sealed class ConcurrencyConflictException(string entityType) : Exception(
    $"The {entityType} was modified by someone else since it was last read - refetch and retry.")
{
    public string EntityType { get; } = entityType;
}
