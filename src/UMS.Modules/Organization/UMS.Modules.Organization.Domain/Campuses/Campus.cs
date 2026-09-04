using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Events;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.Domain.Campuses;

/// <summary>A physical site of the University (glossary). Uniqueness is scoped to its parent University (edge-cases.md, "Uniqueness Enforcement Mechanism") via a DB-level composite unique constraint on <c>(university_id, name)</c>.</summary>
public sealed class Campus : AggregateRoot<CampusId>
{
    private Campus()
    {
    }

    private Campus(CampusId id, UniversityId universityId, string name, DateTimeOffset now)
    {
        Id = id;
        UniversityId = universityId;
        Name = name;
        Status = NodeStatus.Active;
        CreatedAt = now;
    }

    public UniversityId UniversityId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public NodeStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates a new Campus under <paramref name="universityId"/>. The caller (CampusManagementService)
    /// is responsible for the parent-exists-and-is-active check under a row lock before calling
    /// this factory (design-decisions.md, "Parent-Active-Status Validation Mechanism") - the
    /// aggregate itself has no way to query the parent's current state, matching the same
    /// division of responsibility <c>User.Provision</c> already establishes in Identity.
    /// </summary>
    public static Campus Create(UniversityId universityId, string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Campus name is required.", nameof(name));
        }

        return new Campus(CampusId.New(), universityId, name.Trim(), now);
    }

    public bool Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("Campus name is required.", nameof(newName));
        }

        var trimmed = newName.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var previous = Name;
        Name = trimmed;
        Raise(new OrganizationNodeRenamed(OrganizationNodeType.Campus, Id.Value, previous, trimmed, now));
        return true;
    }

    /// <summary>Cascade-block check (any active Faculty beneath this Campus) is performed by the calling Application service under the parent row lock - see <see cref="University.Deactivate"/>'s own remarks for why this lives off the aggregate.</summary>
    public void Deactivate()
    {
        if (Status == NodeStatus.Inactive)
        {
            throw new InvalidOperationException("Campus is already inactive.");
        }

        Status = NodeStatus.Inactive;
    }

    public void Activate()
    {
        if (Status == NodeStatus.Active)
        {
            throw new InvalidOperationException("Campus is already active.");
        }

        Status = NodeStatus.Active;
    }
}
