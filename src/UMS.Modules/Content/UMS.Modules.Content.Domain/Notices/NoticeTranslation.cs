namespace UMS.Modules.Content.Domain.Notices;

/// <summary>
/// ADR-0011, copying Organization's exact <c>NameTranslation</c> shape: one
/// `(languageCode, title, body)` row in `notice_translations`, holding ONLY a non-English override
/// - <see cref="Notice.Title"/>/<see cref="Notice.Body"/> are always the canonical English values
/// (used for the English-fallback read path and the bilingual-completeness gate's own English-half
/// check). A translation row exists only when a translator has supplied a non-English (in practice,
/// Bengali) override.
/// </summary>
public sealed class NoticeTranslation
{
    private NoticeTranslation()
    {
    }

    private NoticeTranslation(string languageCode, string title, string body)
    {
        LanguageCode = languageCode;
        Title = title;
        Body = body;
    }

    public string LanguageCode { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public static NoticeTranslation Create(string languageCode, string title, string body)
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

        return new NoticeTranslation(languageCode.Trim().ToLowerInvariant(), title.Trim(), body.Trim());
    }

    internal void Update(string title, string body)
    {
        Title = title.Trim();
        Body = body.Trim();
    }
}
