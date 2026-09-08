namespace UMS.Modules.Content.Application.Abstractions;

/// <summary>Thrown by the DbContext when an `xmin`-guarded write loses an optimistic-concurrency race - design-decisions.md "Concurrent-Edit Conflict Resolution," edge-cases.md "Two admins concurrently editing the same Notice/Banner" AND "A scheduled publish_at firing while an admin is mid-edit" (the scheduled job is just another writer that can throw this too). Mirrors every other module's own copy exactly.</summary>
public sealed class ConcurrencyConflictException(string entityType) : Exception(
    $"The {entityType} was modified by someone else since it was last read - refetch and retry.")
{
    public string EntityType { get; } = entityType;
}
