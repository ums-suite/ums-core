using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Translations;

namespace UMS.Modules.Organization.Domain.Designations;

/// <summary>
/// A named staff/faculty position/title (glossary: "value object" in ubiquitous-language.md, but
/// requirement-spec.md organization §3 notes it "needs its own CRUD/table" and §6 lists it as a
/// listable/creatable resource with its own permission - modeled here as a small-identity entity,
/// per the module's own build-brief resolution of that tension. It never joins the University→
/// Campus→Faculty→Department→Program parent chain (ORG-6 has no dependency, tickets.md's Sprint
/// Planner notes), and per tickets.md's Flagged Gaps has no `PATCH`/deactivate endpoint - it is
/// create-and-list only, so it carries no <see cref="Common.NodeStatus"/> and raises no domain
/// event (there is no `DesignationCreated` in requirement-spec.md §3's events table).
/// </summary>
public sealed class Designation : AggregateRoot<DesignationId>
{
    private readonly List<NameTranslation> _translations = [];

    private Designation()
    {
    }

    private Designation(DesignationId id, string title, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        CreatedAt = now;
    }

    public string Title { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<NameTranslation> Translations => _translations.AsReadOnly();

    public static Designation Create(string title, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Designation title is required.", nameof(title));
        }

        return new Designation(DesignationId.New(), title.Trim(), now);
    }

    /// <summary>Set at creation time only (no `PATCH` exists for Designation) - accepted as an optional part of the `POST /designations` request body.</summary>
    public void SetTranslation(string languageCode, string title)
    {
        var translation = NameTranslation.Create(languageCode, title);
        var existingIndex = _translations.FindIndex(t => string.Equals(t.LanguageCode, translation.LanguageCode, StringComparison.OrdinalIgnoreCase));
        if (existingIndex >= 0)
        {
            _translations[existingIndex].UpdateName(translation.Name);
        }
        else
        {
            _translations.Add(translation);
        }
    }

    public string ResolveTitle(string? languageCode) => NameLocalizer.Resolve(Title, _translations, languageCode);
}
