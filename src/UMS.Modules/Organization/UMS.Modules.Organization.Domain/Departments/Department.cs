using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Events;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Modules.Organization.Domain.Translations;

namespace UMS.Modules.Organization.Domain.Departments;

/// <summary>
/// An academic unit within a Faculty, owning Programs (glossary). Uniqueness is scoped to its
/// parent Faculty (edge-cases.md, "Uniqueness Enforcement Mechanism") via a DB-level composite
/// unique constraint on <c>(faculty_id, name)</c> - "two Departments named 'Computer Science' can
/// exist under two different Faculties" (requirement-spec.md organization §4) is legal by design.
/// </summary>
public sealed class Department : AggregateRoot<DepartmentId>
{
    private readonly List<NameTranslation> _translations = [];

    private Department()
    {
    }

    private Department(DepartmentId id, FacultyId facultyId, string name, DateTimeOffset now)
    {
        Id = id;
        FacultyId = facultyId;
        Name = name;
        Status = NodeStatus.Active;
        CreatedAt = now;
    }

    public FacultyId FacultyId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public NodeStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<NameTranslation> Translations => _translations.AsReadOnly();

    public static Department Create(FacultyId facultyId, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Department name is required.", nameof(name));
        }

        var department = new Department(DepartmentId.New(), facultyId, name.Trim(), now);
        department.Raise(new DepartmentCreated(department.Id.Value, facultyId.Value, department.Name, now));
        return department;
    }

    public bool Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Department name is required.", nameof(newName));
        }

        var trimmed = newName.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var previous = Name;
        Name = trimmed;
        Raise(new OrganizationNodeRenamed(OrganizationNodeType.Department, Id.Value, previous, trimmed, now));
        return true;
    }

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
    /// requirement-spec.md organization §4/§8: the caller (DepartmentManagementService) must have
    /// already verified, under the parent Faculty's row lock, both that zero active Programs exist
    /// beneath this Department *and* (edge-cases.md "Deactivating a Department that still has a
    /// Faculty member on record") that Faculty's own (stub, until Flow #10) employment-check
    /// interface reports no active FacultyMember still attached, re-checked immediately before
    /// commit (design-decisions.md, "Cross-Module Hard-Delete Reference Check Timing" - the same
    /// resolution pattern, applied here to a deactivation rather than a hard delete).
    /// </summary>
    public void Deactivate(DateTimeOffset now)
    {
        if (Status == NodeStatus.Inactive)
        {
            throw new InvalidOperationException("Department is already inactive.");
        }

        Status = NodeStatus.Inactive;
        Raise(new DepartmentDeactivated(Id.Value, now));
    }
}
