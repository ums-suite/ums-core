namespace UMS.Modules.Hostel.Application.Abstractions;

/// <summary>Thrown by the DbContext when an `xmin`-guarded ordinary write loses an optimistic-concurrency race. Mirrors every other module's own copy exactly.</summary>
public sealed class ConcurrencyConflictException(string entityType) : Exception(
    $"The {entityType} was modified by someone else since it was last read - refetch and retry.")
{
    public string EntityType { get; } = entityType;
}
