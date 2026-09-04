namespace UMS.Modules.Audit.Application.Retention;

/// <summary>
/// The Postgres-specific port <see cref="AuditRetentionEnforcementService"/> depends on - kept
/// separate from that service so the archival *decision* (<see cref="RetentionPolicyEvaluator"/>)
/// and the archival *orchestration* (this service) can both be unit tested with a fake, without a
/// real Postgres instance. <c>UMS.Modules.Audit.Infrastructure</c>'s
/// <c>PostgresPartitionInspector</c> is the one real implementation.
/// </summary>
public interface IPartitionInspector
{
    public Task<IReadOnlyList<PartitionInfo>> ListPartitionsAsync(CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<string>> GetDistinctEntityTypesAsync(string partitionName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detaches (never drops) a partition (design-decisions.md, "Archival/Retention Job Design":
    /// "detach and move to cold/compressed storage" - the actual cold-storage move is a separate,
    /// out-of-band ops step this method does not perform).
    /// </summary>
    public Task DetachPartitionAsync(string partitionName, CancellationToken cancellationToken = default);
}
