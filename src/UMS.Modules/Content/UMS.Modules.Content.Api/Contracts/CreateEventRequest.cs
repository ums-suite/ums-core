namespace UMS.Modules.Content.Api.Contracts;

public sealed record CreateEventRequest(string Title, string Body, string? LocationLabel, string[] Audience, Guid? OrganizationNodeId, DateTimeOffset StartAt, DateTimeOffset EndAt);

public sealed record UpdateEventContentRequest(string Title, string Body, string? LocationLabel, DateTimeOffset StartAt, DateTimeOffset EndAt, uint Version);

public sealed record UpsertEventTranslationRequest(string LanguageCode, string Title, string Body, string? LocationLabel, uint Version);
