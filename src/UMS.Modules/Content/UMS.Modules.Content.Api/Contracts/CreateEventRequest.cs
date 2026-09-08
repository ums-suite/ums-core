namespace UMS.Modules.Content.Api.Contracts;

/// <param name="TranslationLanguageCode">
/// Optional inline non-English (`"bn"`) translation - see <c>Event.Create</c>'s own remarks:
/// Event has no Draft state to add a translation into after the fact before some later publish
/// transition (it has none), so this is the only way a Public-audience Event can ever be created
/// bilingual-complete in one call.
/// </param>
public sealed record CreateEventRequest(string Title, string Body, string? LocationLabel, string[] Audience, Guid? OrganizationNodeId, DateTimeOffset StartAt, DateTimeOffset EndAt, string? TranslationLanguageCode = null, string? TranslationTitle = null, string? TranslationBody = null, string? TranslationLocationLabel = null);

public sealed record UpdateEventContentRequest(string Title, string Body, string? LocationLabel, DateTimeOffset StartAt, DateTimeOffset EndAt, uint Version);

public sealed record UpsertEventTranslationRequest(string LanguageCode, string Title, string Body, string? LocationLabel, uint Version);
