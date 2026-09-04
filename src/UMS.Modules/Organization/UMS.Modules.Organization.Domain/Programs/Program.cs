using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Events;
using UMS.Modules.Organization.Domain.Translations;

namespace UMS.Modules.Organization.Domain.Programs;

/// <summary>
/// The bare structural record of a degree offering (requirement-spec.md organization §9.1:
/// "Organization owns Program's bare structural record (id, name, department link, status) ...
/// Academic's own `Program` aggregate ... is the same row extended with Curriculum ownership,
/// accessed through Academic's own application-service interface rather than a second table").
/// Uniqueness is scoped to its parent Department (edge-cases.md, "Uniqueness Enforcement
/// Mechanism") via a DB-level composite unique constraint on <c>(department_id, name)</c>.
/// </summary>
public sealed class Program : AggregateRoot<ProgramId>
{
    private readonly List<NameTranslation> _translations = [];

    private Program()
    {
    }

    private Program(ProgramId id, DepartmentId departmentId, string name, DateTimeOffset now)
    {
        Id = id;
        DepartmentId = departmentId;
        Name = name;
        Status = NodeStatus.Active;
        CreatedAt = now;
    }

    public DepartmentId DepartmentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public NodeStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<NameTranslation> Translations => _translations.AsReadOnly();

    /// <summary>
    /// edge-cases.md/requirement-spec.md §8: "A Program created before its Department is fully
    /// configured ... Legal - Program creation only requires an existing, active Department" -
    /// this factory has no dependency on Designation/staffing state at all, by design.
    /// </summary>
    public static Program Create(DepartmentId departmentId, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Program name is required.", nameof(name));
        }

        var program = new Program(ProgramId.New(), departmentId, name.Trim(), now);

        // requirement-spec.md organization §3: dispatched to Audit synchronously *and* to the
        // outbox as an informational notification to Academic - see ProgramCreated's own remarks.
        program.Raise(new ProgramCreated(program.Id.Value, departmentId.Value, program.Name, now));
        return program;
    }

    public bool Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Program name is required.", nameof(newName));
        }

        var trimmed = newName.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var previous = Name;
        Name = trimmed;
        Raise(new OrganizationNodeRenamed(OrganizationNodeType.Program, Id.Value, previous, trimmed, now));
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

    public void Deactivate(DateTimeOffset now)
    {
        if (Status == NodeStatus.Inactive)
        {
            throw new InvalidOperationException("Program is already inactive.");
        }

        Status = NodeStatus.Inactive;
        Raise(new ProgramDeactivated(Id.Value, now));
    }
}
