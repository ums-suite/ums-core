using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace UMS.Modules.Library.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists a simple primitive list as one <c>jsonb</c> column rather than a child table - mirrors
/// Hostel's own <c>JsonListConverters</c> exactly: nothing in this module ever queries/filters/joins
/// on one individual <c>Book.AuthorIds</c> element, so a jsonb column costs nothing here versus a
/// many-to-many join table.
/// </summary>
internal static class JsonListConverters
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static ValueConverter<IReadOnlyCollection<Guid>, string> GuidList { get; } = new(
        list => JsonSerializer.Serialize(list, SerializerOptions),
        json => JsonSerializer.Deserialize<List<Guid>>(json, SerializerOptions) ?? new List<Guid>());

    public static ValueComparer<IReadOnlyCollection<Guid>> GuidListComparer { get; } = new(
        (left, right) => (left ?? new List<Guid>()).SequenceEqual(right ?? new List<Guid>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());
}
