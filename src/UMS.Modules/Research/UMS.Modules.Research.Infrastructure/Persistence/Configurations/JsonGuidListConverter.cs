using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace UMS.Modules.Research.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists <c>Publication.FundedByGrantIds</c> as one <c>jsonb</c> column rather than a join table -
/// mirrors Admission's own <c>JsonListConverters.GuidList</c> exactly (its own remarks explain why:
/// requirement-spec.md §3/§9 "cross-aggregate references by id only, never an object graph load" -
/// nothing in this module ever needs to join through this list at the SQL level).
///
/// <para>
/// Modeled as <see cref="IReadOnlyCollection{T}"/> (never the concrete <c>List&lt;T&gt;</c>) because
/// EF Core's design-time validation requires a value converter's declared source type to match the
/// mapped PROPERTY's own exposed CLR type exactly - even under <c>PropertyAccessMode.Field</c>.
/// </para>
/// </summary>
internal static class JsonGuidListConverter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static ValueConverter<IReadOnlyCollection<Guid>, string> Converter { get; } = new(
        list => JsonSerializer.Serialize(list, SerializerOptions),
        json => JsonSerializer.Deserialize<List<Guid>>(json, SerializerOptions) ?? new List<Guid>());

    public static ValueComparer<IReadOnlyCollection<Guid>> Comparer { get; } = new(
        (left, right) => (left ?? new List<Guid>()).SequenceEqual(right ?? new List<Guid>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());
}
