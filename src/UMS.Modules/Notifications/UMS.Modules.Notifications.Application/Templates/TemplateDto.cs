using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Templates;

namespace UMS.Modules.Notifications.Application.Templates;

public sealed record TemplateDto(
    Guid Id,
    string EventType,
    NotificationChannel Channel,
    bool IsActive,
    int Version,
    IReadOnlyList<TemplateTranslationDto> Translations)
{
    public static TemplateDto FromDomain(Template template) => new(
        template.Id.Value,
        template.EventType,
        template.Channel,
        template.IsActive,
        template.Version,
        [.. template.Translations.Select(t => new TemplateTranslationDto(t.LanguageCode, t.Subject, t.Body, t.PushTitle, t.DeepLink))]);
}

public sealed record TemplateTranslationDto(string LanguageCode, string? Subject, string Body, string? PushTitle, string? DeepLink);

public sealed record UpsertTemplateTranslationCommand(string LanguageCode, string? Subject, string Body, string? PushTitle, string? DeepLink);
