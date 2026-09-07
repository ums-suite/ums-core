using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using UMS.Modules.Learning.Domain.Assignments;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// Persists a <see cref="LatePenaltyPolicy"/> as one <c>jsonb</c> column rather than a child table.
///
/// <para>
/// The tier schedule is a value object nested inside another value object
/// (<see cref="SubmissionWindow"/>): it is always read and written as a whole with its owning
/// window, and nothing in this module ever queries, filters, or joins on an individual tier. A
/// child table would buy nothing and would make <see cref="SubmissionWindow"/> - a value object -
/// carry an identity it does not have.
/// </para>
///
/// <para>
/// The comparer is required, not optional: without it EF's change tracker would compare policies by
/// reference and silently miss an edit to the schedule.
/// </para>
/// </summary>
internal static class LatePenaltyPolicyConverter
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    public static ValueConverter<LatePenaltyPolicy, string> Converter { get; } = new(
        policy => Serialize(policy),
        json => Deserialize(json));

    public static ValueComparer<LatePenaltyPolicy> Comparer { get; } = new(
        (left, right) => Serialize(left) == Serialize(right),
        policy => Serialize(policy).GetHashCode(StringComparison.Ordinal),
        policy => Deserialize(Serialize(policy)));

    private static string Serialize(LatePenaltyPolicy? policy) =>
        JsonSerializer.Serialize(
            (policy ?? LatePenaltyPolicy.NoDeduction).Tiers.Select(t => new PersistedTier(t.MaxLateness, t.DeductionPercentage)).ToList(),
            SerializerOptions);

    private static LatePenaltyPolicy Deserialize(string json)
    {
        var persisted = JsonSerializer.Deserialize<List<PersistedTier>>(json, SerializerOptions) ?? [];
        var tiers = new List<LatePenaltyTier>(persisted.Count);

        foreach (var tier in persisted)
        {
            var created = LatePenaltyTier.Create(tier.MaxLateness, tier.DeductionPercentage);
            if (created.IsSuccess)
            {
                tiers.Add(created.Value);
            }
        }

        var policy = LatePenaltyPolicy.Create(tiers);
        return policy.IsSuccess ? policy.Value : LatePenaltyPolicy.NoDeduction;
    }

    private sealed record PersistedTier(TimeSpan MaxLateness, decimal DeductionPercentage);
}
