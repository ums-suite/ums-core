using UMS.Modules.Content.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Domain.HomepageSections;

/// <summary>
/// CNT-10: glossary (module-local term pending glossary merge, requirement-spec.md §9) "an ordered,
/// toggleable homepage layout block; may reference another module's entity by id."
/// requirement-spec.md §2.5: a homepage section referencing another module's data (e.g. "featured
/// programs") stores ONLY a reference (Program id) resolved at read time via Organization's public
/// query interface - never a denormalized copy (ADR-0002). edge-cases.md "Organization reference
/// later deleted/deactivated": the application layer resolving <see cref="ReferenceOrganizationNodeId"/>
/// at read time must degrade gracefully, never throw.
/// </summary>
public sealed class HomepageSection : AggregateRoot<HomepageSectionId>, IDisplayOrderable
{
    private HomepageSection()
    {
    }

    private HomepageSection(HomepageSectionId id, string sectionKey, string title, int sortOrder, bool isEnabled, Guid? referenceOrganizationNodeId, DateTimeOffset now)
    {
        Id = id;
        SectionKey = sectionKey;
        Title = title;
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        ReferenceOrganizationNodeId = referenceOrganizationNodeId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>A stable, Admin-chosen slug, e.g. `"hero"`/`"featured-notices"`/`"upcoming-events"`/`"featured-programs"` (requirement-spec.md §2.5).</summary>
    public string SectionKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsEnabled { get; private set; }

    /// <summary>Null when this section carries no cross-module reference (e.g. a plain hero banner rail).</summary>
    public Guid? ReferenceOrganizationNodeId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<HomepageSection> Create(string sectionKey, string title, int sortOrder, bool isEnabled, Guid? referenceOrganizationNodeId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
        {
            return Error.Validation("homepage_section.key_required", "A HomepageSection requires a section key.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("homepage_section.title_required", "A HomepageSection requires a title.");
        }

        return new HomepageSection(HomepageSectionId.New(), sectionKey.Trim().ToLowerInvariant(), title.Trim(), sortOrder, isEnabled, referenceOrganizationNodeId, now);
    }

    public Result UpdateDetails(string title, int sortOrder, bool isEnabled, Guid? referenceOrganizationNodeId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("homepage_section.title_required", "A HomepageSection requires a title."));
        }

        Title = title.Trim();
        SortOrder = sortOrder;
        IsEnabled = isEnabled;
        ReferenceOrganizationNodeId = referenceOrganizationNodeId;
        UpdatedAt = now;
        return Result.Success();
    }
}
