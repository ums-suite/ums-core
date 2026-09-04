using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Domain.Templates;

/// <summary>
/// requirement-spec.md §3: "Localized, channel-specific message template". Keyed by
/// (<see cref="EventType"/>, <see cref="Channel"/>) - matches edge-cases.md's own wording, "the
/// requested (eventType, channel, language) template combination". §9 Open Questions: the full
/// enumerated (eventType, channel) catalog is deliberately NOT a fixed enum - this is the data-
/// driven, admin-configurable registry mechanism (NTF-7/NTF-8) that catalog lives in.
/// </summary>
public sealed class Template : AggregateRoot<TemplateId>
{
    public const string EnglishLanguageCode = "en";

    private readonly List<TemplateTranslation> _translations = [];

    private Template()
    {
    }

    public string EventType { get; private set; } = string.Empty;

    public NotificationChannel Channel { get; private set; }

    public bool IsActive { get; private set; }

    public int Version { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<TemplateTranslation> Translations => _translations.AsReadOnly();

    public static Result<Template> Create(string eventType, NotificationChannel channel, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return Error.Validation("template.event_type_required", "Event type is required.");
        }

        return new Template
        {
            Id = TemplateId.New(),
            EventType = eventType.Trim(),
            Channel = channel,
            IsActive = true,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>NTF-8's <c>PUT /notifications/templates/{id}</c> - creates or replaces one language's translation. Bumps <see cref="Version"/> every call (requirement-spec.md §2: "versioned").</summary>
    public Result UpsertTranslation(string languageCode, string? subject, string body, string? pushTitle, string? deepLink, DateTimeOffset now)
    {
        var translationResult = TemplateTranslation.Create(languageCode, Channel, subject, body, pushTitle, deepLink, now);
        if (translationResult.IsFailure)
        {
            return Result.Failure(translationResult.Error!);
        }

        _translations.RemoveAll(t => t.LanguageCode == translationResult.Value.LanguageCode);
        _translations.Add(translationResult.Value);
        Version++;
        UpdatedAt = now;
        return Result.Success();
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>
    /// design-decisions.md, "Template Resolution Fallback - Server-Side English Fallback, Never a
    /// Blank Send": resolves the requested language, falling back to
    /// <see cref="EnglishLanguageCode"/> if missing. Returns <c>null</c> only if English itself is
    /// also missing - the caller (the dispatch pipeline) turns that into an immediate
    /// <see cref="DeadLetterReason.TemplateMissing"/>, never a blank send.
    /// </summary>
    public TemplateTranslation? Resolve(string languageCode)
    {
        var normalized = languageCode.Trim().ToLowerInvariant();
        return _translations.FirstOrDefault(t => t.LanguageCode == normalized)
            ?? _translations.FirstOrDefault(t => t.LanguageCode == EnglishLanguageCode);
    }
}
