using UMS.Modules.Content.Domain.Common;

namespace UMS.Modules.Content.UnitTests.Common;

/// <summary>
/// design-decisions.md "Deterministic Ordering Tiebreaker": `sort_order` ascending, ties broken by
/// `created_at` ascending - ONE rule, tested directly against the shared
/// <see cref="ContentOrdering"/> helper rather than duplicated per entity.
/// </summary>
public sealed class ContentOrderingTests
{
    private sealed record Item(string Name, int SortOrder, DateTimeOffset CreatedAt) : IDisplayOrderable;

    [Fact]
    public void Orders_by_sort_order_ascending_first()
    {
        var now = DateTimeOffset.UtcNow;
        var items = new[] { new Item("B", 2, now), new Item("A", 1, now) };

        var ordered = items.ByDisplayOrder().Select(i => i.Name).ToList();

        Assert.Equal(["A", "B"], ordered);
    }

    [Fact]
    public void Ties_on_sort_order_are_broken_by_created_at_ascending()
    {
        // edge-cases.md "Two Banners' active windows overlap at the same sort_order" - the older
        // one (by created_at) must display first, deterministically, never "whatever order the DB
        // happens to return."
        var older = new Item("Older", 1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = new Item("Newer", 1, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero));

        var ordered = new[] { newer, older }.ByDisplayOrder().Select(i => i.Name).ToList();

        Assert.Equal(["Older", "Newer"], ordered);
    }
}
