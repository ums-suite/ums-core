using Microsoft.Extensions.Options;

namespace UMS.Modules.Audit.Application.Retention;

/// <summary>
/// AUD-4's pure decision logic, kept deliberately free of any database access so it can be unit
/// tested without a real partition (design-decisions.md, "Archival/Retention Job Design": "a
/// partition holding even one indefinite-retention entity type... is never archived away, only
/// ever compressed/moved as a whole once all its content has aged past policy").
/// </summary>
public sealed class RetentionPolicyEvaluator(IOptions<AuditRetentionOptions> options)
{
    /// <summary>
    /// True only when every entity type observed in the partition has both a configured
    /// (non-indefinite) retention window AND that window has already elapsed as of
    /// <paramref name="asOf"/>, measured from the partition's own end boundary.
    /// </summary>
    public bool IsPartitionArchivable(
        DateTimeOffset partitionEndExclusive,
        IReadOnlyCollection<string> entityTypesInPartition,
        DateTimeOffset asOf)
    {
        foreach (var entityType in entityTypesInPartition)
        {
            if (!options.Value.EntityTypeRetentionMonths.TryGetValue(entityType, out var retentionMonths))
            {
                // No configured window -> indefinite retention (the safe default) -> never archivable.
                return false;
            }

            var retentionExpiresAt = partitionEndExclusive.AddMonths(retentionMonths);
            if (retentionExpiresAt > asOf)
            {
                return false;
            }
        }

        // An empty partition (no rows of any entity type) has nothing left to retain.
        return true;
    }
}
