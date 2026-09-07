using UMS.Modules.Learning.Domain.Common;

namespace UMS.Modules.Learning.Domain.LectureMaterials;

/// <summary>
/// One language's title/description for a <see cref="LectureMaterial"/> - the platform-wide
/// <c>{table}_translations</c> shape keyed <c>(entity_id, language_code)</c> that
/// ums-conventions.md's Localization Implementation section fixes for every module (ADR-0011).
/// Resolved server-side with English as the universal fallback; never both languages shipped to the
/// browser for client-side resolution.
/// </summary>
public sealed class LectureMaterialTranslation
{
    private LectureMaterialTranslation()
    {
    }

    public LectureMaterialId LectureMaterialId { get; private init; }

    public LanguageCode Language { get; private init; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    internal static LectureMaterialTranslation Create(LectureMaterialId lectureMaterialId, LanguageCode language, string title, string? description) =>
        new()
        {
            LectureMaterialId = lectureMaterialId,
            Language = language,
            Title = title,
            Description = description,
        };

    internal void Update(string title, string? description)
    {
        Title = title;
        Description = description;
    }
}
