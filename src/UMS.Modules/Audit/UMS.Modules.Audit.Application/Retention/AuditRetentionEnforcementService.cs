using UMS.Modules.Audit.Application.Abstractions;

namespace UMS.Modules.Audit.Application.Retention;

/// <summary>
/// AUD-4's orchestration: for every partition that has fully aged out of the active window, ask
/// <see cref="RetentionPolicyEvaluator"/> whether every entity type it holds has individually
/// passed its own configured retention window, and if so, detach it (design-decisions.md,
/// "Archival/Retention Job Design"). Never touches the current or any future partition - those are
/// still being written to (edge-cases.md's "retention/archival job races an in-flight read or
/// export" is the reason this is a deliberate, explicit call rather than a tight polling loop -
/// see this module's own README/PR notes for the operational scheduling this still needs).
/// </summary>
public sealed class AuditRetentionEnforcementService(IPartitionInspector inspector, RetentionPolicyEvaluator evaluator, IClock clock)
{
    public async Task<IReadOnlyList<string>> ArchiveEligiblePartitionsAsync(CancellationToken cancellationToken = default)
    {
        var asOf = clock.UtcNow;
        var archived = new List<string>();

        foreach (var partition in await inspector.ListPartitionsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (partition.RangeEndExclusive > asOf)
            {
                // Still the current (or a future) partition - never a candidate, regardless of policy.
                continue;
            }

            var entityTypes = await inspector.GetDistinctEntityTypesAsync(partition.Name, cancellationToken).ConfigureAwait(false);
            if (!evaluator.IsPartitionArchivable(partition.RangeEndExclusive, entityTypes, asOf))
            {
                continue;
            }

            await inspector.DetachPartitionAsync(partition.Name, cancellationToken).ConfigureAwait(false);
            archived.Add(partition.Name);
        }

        return archived;
    }
}
