namespace UMS.Modules.Content.Api.Contracts;

public sealed record CreateHomepageSectionRequest(string SectionKey, string Title, int SortOrder, bool IsEnabled, Guid? ReferenceOrganizationNodeId);

public sealed record UpdateHomepageSectionRequest(string Title, int SortOrder, bool IsEnabled, Guid? ReferenceOrganizationNodeId, uint Version);
