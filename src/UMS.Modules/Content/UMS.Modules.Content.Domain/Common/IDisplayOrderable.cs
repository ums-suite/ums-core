namespace UMS.Modules.Content.Domain.Common;

/// <summary>
/// design-decisions.md "Deterministic Ordering Tiebreaker": the one shape
/// <see cref="Banners.Banner"/> and <see cref="HomepageSections.HomepageSection"/> both implement so
/// a single ordering rule (<see cref="ContentOrdering.ByDisplayOrder{T}"/>) applies uniformly to
/// both, rather than two parallel hand-written `OrderBy` chains that could drift out of sync.
/// </summary>
public interface IDisplayOrderable
{
    public int SortOrder { get; }

    public DateTimeOffset CreatedAt { get; }
}

/// <summary>`sort_order` ascending, ties broken by `created_at` ascending (requirement-spec.md §4; edge-cases.md "Two Banners' active windows overlap at the same sort_order") - the ONE tiebreaker rule, applied identically to Banner display priority and HomepageSection ordering.</summary>
public static class ContentOrdering
{
    public static IOrderedEnumerable<T> ByDisplayOrder<T>(this IEnumerable<T> items)
        where T : IDisplayOrderable =>
        items.OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt);

    public static IOrderedQueryable<T> ByDisplayOrder<T>(this IQueryable<T> items)
        where T : IDisplayOrderable =>
        items.OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt);
}
