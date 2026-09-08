namespace UMS.Modules.Content.Domain.Events;

/// <summary>ADR-0011, the same shape as <see cref="Notices.NoticeTranslation"/> extended with an optional translated `LocationLabel` (requirement-spec.md §2.4: "title, body, location label").</summary>
public sealed class EventTranslation
{
    private EventTranslation()
    {
    }

    private EventTranslation(string languageCode, string title, string body, string? locationLabel)
    {
        LanguageCode = languageCode;
        Title = title;
        Body = body;
        LocationLabel = locationLabel;
    }

    public string LanguageCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? LocationLabel { get; private set; }

    public static EventTranslation Create(string languageCode, string title, string body, string? locationLabel)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("A translation's language code is required.", nameof(languageCode));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A translation's title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("A translation's body is required.", nameof(body));
        }

        return new EventTranslation(languageCode.Trim().ToLowerInvariant(), title.Trim(), body.Trim(), string.IsNullOrWhiteSpace(locationLabel) ? null : locationLabel.Trim());
    }

    internal void Update(string title, string body, string? locationLabel)
    {
        Title = title.Trim();
        Body = body.Trim();
        LocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? null : locationLabel.Trim();
    }
}
