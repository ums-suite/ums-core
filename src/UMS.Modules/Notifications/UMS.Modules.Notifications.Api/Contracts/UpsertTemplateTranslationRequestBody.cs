namespace UMS.Modules.Notifications.Api.Contracts;

public sealed record UpsertTemplateTranslationRequestBody(string LanguageCode, string? Subject, string Body, string? PushTitle, string? DeepLink);
