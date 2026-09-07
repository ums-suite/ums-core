using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists a simple primitive list (<c>Guid</c>/<c>string</c>) as one <c>jsonb</c> column rather
/// than a child table - mirrors Learning's own <c>LatePenaltyPolicyConverter</c> exactly (its own
/// remarks explain why: nothing in this module ever queries/filters/joins on an individual element,
/// so a child table would buy nothing).
///
/// <para>
/// Modeled as <see cref="IReadOnlyCollection{T}"/> (never the concrete <c>List&lt;T&gt;</c>) because
/// EF Core's design-time validation requires a value converter's declared source type to match the
/// mapped PROPERTY's own exposed CLR type exactly - even under <c>PropertyAccessMode.Field</c>,
/// which only changes how a value is read/written at runtime, not the model's declared type.
/// </para>
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

    public static ValueConverter<IReadOnlyCollection<string>, string> StringList { get; } = new(
        list => JsonSerializer.Serialize(list, SerializerOptions),
        json => JsonSerializer.Deserialize<List<string>>(json, SerializerOptions) ?? new List<string>());

    public static ValueComparer<IReadOnlyCollection<string>> StringListComparer { get; } = new(
        (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());
}
