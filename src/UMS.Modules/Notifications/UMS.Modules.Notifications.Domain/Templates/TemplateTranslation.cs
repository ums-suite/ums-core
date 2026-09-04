using UMS.Modules.Notifications.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Domain.Templates;

/// <summary>
/// requirement-spec.md §2 Template Management: "email needs subject + HTML body; SMS is plain-text
/// and character-bounded; push needs title + short body; in-app needs a short body + optional
/// deep-link" - one row of the <c>template_translations</c> table (ums-conventions.md, Localization
/// Implementation: "a <c>{table}_translations</c> table keyed <c>(entity_id, language_code)</c>").
/// </summary>
public sealed class TemplateTranslation
{
    /// <summary>GSM-7 single-SMS-segment bound; a longer body is accepted (multi-segment) up to this ceiling, matching a typical real SMS gateway's own per-message cap.</summary>
    private const int SmsMaxLength = 480;

    private TemplateTranslation()
    {
    }

    public string LanguageCode { get; private set; } = string.Empty;

    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public string? PushTitle { get; private set; }

    public string? DeepLink { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal static Result<TemplateTranslation> Create(string languageCode, NotificationChannel channel, string? subject, string body, string? pushTitle, string? deepLink, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return Error.Validation("template_translation.language_required", "Language code is required.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Error.Validation("template_translation.body_required", "Body is required.");
        }

        if (channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(subject))
        {
            return Error.Validation("template_translation.subject_required", "Email templates require a subject.");
        }

        if (channel == NotificationChannel.Push && string.IsNullOrWhiteSpace(pushTitle))
        {
            return Error.Validation("template_translation.push_title_required", "Push templates require a title.");
        }

        if (channel is NotificationChannel.Sms or NotificationChannel.WhatsApp && body.Length > SmsMaxLength)
        {
            return Error.Validation("template_translation.body_too_long", $"SMS/WhatsApp body must be at most {SmsMaxLength} characters.");
        }

        return new TemplateTranslation
        {
            LanguageCode = languageCode.Trim().ToLowerInvariant(),
            Subject = subject,
            Body = body,
            PushTitle = pushTitle,
            DeepLink = deepLink,
            UpdatedAt = now,
        };
    }
}
