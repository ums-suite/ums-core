using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using UMS.Modules.Hostel.Domain.ApplicationWindows;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists a simple primitive/value-object list as one <c>jsonb</c> column rather than a child
/// table - mirrors Admission's own <c>JsonListConverters</c> exactly: nothing in this module ever
/// queries/filters/joins on an individual <c>EligibleProgramIds</c>/<c>EligibleYears</c>/
/// <c>EligibilityRules</c> element, so a child table (with its own Ordinal-shadow-key ceremony)
/// would buy nothing for this specific, whole-set-read configuration data.
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

    public static ValueConverter<IReadOnlyCollection<int>, string> IntList { get; } = new(
        list => JsonSerializer.Serialize(list, SerializerOptions),
        json => JsonSerializer.Deserialize<List<int>>(json, SerializerOptions) ?? new List<int>());

    public static ValueComparer<IReadOnlyCollection<int>> IntListComparer { get; } = new(
        (left, right) => (left ?? new List<int>()).SequenceEqual(right ?? new List<int>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());

    public static ValueConverter<IReadOnlyCollection<EligibilityRuleDefinition>, string> EligibilityRuleList { get; } = new(
        list => JsonSerializer.Serialize(list, SerializerOptions),
        json => JsonSerializer.Deserialize<List<EligibilityRuleDefinition>>(json, SerializerOptions) ?? new List<EligibilityRuleDefinition>());

    public static ValueComparer<IReadOnlyCollection<EligibilityRuleDefinition>> EligibilityRuleListComparer { get; } = new(
        (left, right) => (left ?? new List<EligibilityRuleDefinition>()).SequenceEqual(right ?? new List<EligibilityRuleDefinition>()),
        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
        list => list.ToList());
}
