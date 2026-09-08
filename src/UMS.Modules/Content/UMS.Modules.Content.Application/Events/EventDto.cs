namespace UMS.Modules.Content.Application.Events;

public sealed record EventDto(
    Guid Id,
    string Title,
    string Body,
    string? LocationLabel,
    string LanguageCode,
    string[] Audience,
    Guid? OrganizationNodeId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record EventListPage(IReadOnlyList<EventDto> Items, int TotalCount, int Skip, int Take);
