using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Events;
using UMS.Modules.Organization.Domain.Translations;

namespace UMS.Modules.Organization.Domain.Faculties;

/// <summary>
/// An academic grouping of Departments (glossary) - not to be confused with `FacultyMember`
/// (a person, owned by the `Faculty` module). Uniqueness is scoped to its parent Campus
/// (edge-cases.md, "Uniqueness Enforcement Mechanism") via a DB-level composite unique constraint
/// on <c>(campus_id, name)</c>.
/// </summary>
public sealed class Faculty : AggregateRoot<FacultyId>
{
    private readonly List<NameTranslation> _translations = [];

    private Faculty()
    {
    }

    private Faculty(FacultyId id, CampusId campusId, string name, DateTimeOffset now)
    {
        Id = id;
        CampusId = campusId;
        Name = name;
        Status = NodeStatus.Active;
        CreatedAt = now;
    }

    public CampusId CampusId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public NodeStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<NameTranslation> Translations => _translations.AsReadOnly();

    /// <summary>
    /// Creates a new Faculty under <paramref name="campusId"/>. Per <see cref="Campus.Create"/>'s
    /// own remarks, the parent-exists-and-is-active check under a row lock is the calling
    /// Application service's responsibility, not the aggregate's.
    /// </summary>
    public static Faculty Create(CampusId campusId, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Faculty name is required.", nameof(name));
        }

        var faculty = new Faculty(FacultyId.New(), campusId, name.Trim(), now);
        faculty.Raise(new FacultyCreated(faculty.Id.Value, campusId.Value, faculty.Name, now));
        return faculty;
    }

    public bool Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Faculty name is required.", nameof(newName));
        }

        var trimmed = newName.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var previous = Name;
        Name = trimmed;
        Raise(new OrganizationNodeRenamed(OrganizationNodeType.Faculty, Id.Value, previous, trimmed, now));
        return true;
    }

    /// <summary>ORG-8/ADR-0011: upserts the `(languageCode, name)` translation row - a wholesale replace of that one language's value, matching Identity's own `Role.SetPermissions` "bundle" semantics rather than a separate add/remove API per language.</summary>
    public void SetTranslation(string languageCode, string name)
    {
        var translation = NameTranslation.Create(languageCode, name);
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

    public string ResolveName(string? languageCode) => NameLocalizer.Resolve(Name, _translations, languageCode);

    /// <summary>
    /// requirement-spec.md organization §4 "Deactivation cascades are blocked, not silent" - the
    /// caller (FacultyManagementService) must have already verified, under the same parent row
    /// lock, that zero active Departments exist beneath this Faculty before calling this method;
    /// the aggregate itself cannot see its own children (they live in a different table/aggregate
    /// entirely), so it cannot enforce that check itself.
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        if (Status == NodeStatus.Inactive)
        {
            throw new InvalidOperationException("Faculty is already inactive.");
        }

        Status = NodeStatus.Inactive;
        Raise(new FacultyDeactivated(Id.Value, now));
    }
}
