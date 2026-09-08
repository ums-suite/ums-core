namespace UMS.Modules.Research.Application.Abstractions;

/// <summary>Translated from a Postgres <c>xmin</c> mismatch by the DbContext's own <c>SaveChangesAsync</c> override. Mirrors every other module's own exception exactly.</summary>
public sealed class ConcurrencyConflictException(string entityType) : Exception($"'{entityType}' was modified by another writer - reload and retry.")
{
    public string EntityType { get; } = entityType;
}
