namespace UMS.Modules.Organization.Application.Abstractions;

/// <summary>
/// ORG-9/ORG-10 cache-aside read path (design-decisions.md, "Caching Strategy for the Hierarchy
/// Tree"): Redis-backed, short-TTL cache for the full/subtree hierarchy read and the resolved
/// ancestor-path read, with explicit targeted invalidation on every structural write - never a
/// full-cache flush. <c>null</c> rootId means "the full tree, every University."
/// </summary>
public interface IOrganizationTreeCache
{
    public Task<string?> GetTreeJsonAsync(Guid? rootId, CancellationToken cancellationToken = default);

    public Task SetTreeJsonAsync(Guid? rootId, string json, CancellationToken cancellationToken = default);

    public Task<string?> GetAncestorsJsonAsync(Guid nodeId, CancellationToken cancellationToken = default);

    public Task SetAncestorsJsonAsync(Guid nodeId, string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Targeted invalidation for a mutation to <paramref name="mutatedNodeId"/>: deletes the full-tree
    /// cache entry, the subtree-cache entry for <paramref name="mutatedNodeId"/> itself, the
    /// subtree-cache entry for every id in <paramref name="ancestorIds"/> (the mutated node is part
    /// of each of their subtrees too), and the mutated node's own ancestors-cache entry. When
    /// <paramref name="descendantIds"/> is non-empty (a rename, whose new name affects every
    /// descendant's own breadcrumb), each descendant's ancestors-cache entry is deleted too - see
    /// design-decisions.md's "explicit, targeted invalidation (not a full-cache flush)."
    /// </summary>
    public Task InvalidateAsync(
        Guid mutatedNodeId,
        IReadOnlyCollection<Guid> ancestorIds,
        IReadOnlyCollection<Guid>? descendantIds = null,
        CancellationToken cancellationToken = default);
}
