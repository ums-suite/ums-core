namespace UMS.Modules.Content.Api.Contracts;

public sealed record CreateNoticeRequest(string Title, string Body, string[] Audience, Guid? OrganizationNodeId, bool IsUrgent);

public sealed record EditNoticeContentRequest(string Title, string Body, uint Version);

public sealed record UpsertNoticeTranslationRequest(string LanguageCode, string Title, string Body, uint Version);

public sealed record UpdateNoticeScheduleRequest(DateTimeOffset? PublishAt, DateTimeOffset? ExpireAt, uint Version);

public sealed record NoticeVersionedActionRequest(uint Version);
