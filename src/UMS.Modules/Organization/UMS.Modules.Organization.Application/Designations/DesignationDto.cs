namespace UMS.Modules.Organization.Application.Designations;

public sealed record DesignationDto(Guid Id, string Title, string LocalizedTitle, DateTimeOffset CreatedAt);

public sealed record CreateDesignationRequest(string Title, IReadOnlyDictionary<string, string>? Translations);

public sealed record DesignationListPage(IReadOnlyList<DesignationDto> Items, int TotalCount, int Skip, int Take);
