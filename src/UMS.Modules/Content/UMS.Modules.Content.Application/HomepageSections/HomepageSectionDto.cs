namespace UMS.Modules.Content.Application.HomepageSections;

/// <summary>
/// edge-cases.md "Organization reference later deleted/deactivated": <see cref="ReferenceAvailable"/>
/// is <see langword="null"/> when the section carries no reference at all, <see langword="true"/>/
/// <see langword="false"/> when it does and the reference was resolved/found-missing at read time -
/// never a thrown exception either way.
/// </summary>
public sealed record HomepageSectionDto(
    Guid Id,
    string SectionKey,
    string Title,
    int SortOrder,
    bool IsEnabled,
    Guid? ReferenceOrganizationNodeId,
    bool? ReferenceAvailable,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
