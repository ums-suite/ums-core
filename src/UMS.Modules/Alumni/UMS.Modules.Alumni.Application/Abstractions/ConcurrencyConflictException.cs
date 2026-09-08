namespace UMS.Modules.Alumni.Application.Abstractions;

public sealed class ConcurrencyConflictException(string entityType) : Exception($"A concurrency conflict occurred updating a '{entityType}'.")
{
    public string EntityType { get; } = entityType;
}
