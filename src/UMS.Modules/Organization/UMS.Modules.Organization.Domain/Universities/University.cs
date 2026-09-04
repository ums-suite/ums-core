using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Events;

namespace UMS.Modules.Organization.Domain.Universities;

/// <summary>
/// The root of the organizational hierarchy (glossary: "single-tenant today, structurally ready
/// for multi-tenancy later"; ADR-0015). Exactly one active row is expected in practice, but this
/// is an application-level convention, never a schema-enforced constraint (ADR-0015 explicitly
/// asks each module to name this assumption rather than hard-code it) - <see cref="Create"/> will
/// happily create a second row if asked.
/// </summary>
public sealed class University : AggregateRoot<UniversityId>
{
    private University()
    {
    }

    private University(UniversityId id, string name, string? code, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Code = code;
        Status = NodeStatus.Active;
        CreatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Code { get; private set; }

    public NodeStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static University Create(string name, string? code, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("University name is required.", nameof(name));
        }

        return new University(UniversityId.New(), name.Trim(), code?.Trim(), now);
    }

    /// <summary>Returns whether the name actually changed - the caller only raises an audit entry/cache invalidation when it did (avoids a no-op `PATCH` producing a phantom rename event).</summary>
    public bool Rename(string newName, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("University name is required.", nameof(newName));
        }

        var trimmed = newName.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        var previous = Name;
        Name = trimmed;
        Raise(new OrganizationNodeRenamed(OrganizationNodeType.University, Id.Value, previous, trimmed, now));
        return true;
    }

    public void SetCode(string? code) => Code = code?.Trim();

    /// <summary>
    /// No cascade-block check lives on the aggregate itself - requirement-spec.md organization §4
    /// states the invariant generically ("Deactivation cascades are blocked... a hierarchy it
    /// doesn't fully own the consequences of"), so the row-locked "does an active Campus exist
    /// under this University" check is performed by the calling Application service
    /// (UniversityManagementService), the same layer that holds the parent's `SELECT ... FOR
    /// UPDATE` lock across the whole check-then-transition (design-decisions.md, "Parent-Active-
    /// Status Validation Mechanism").
    /// </summary>
    public void Deactivate()
    {
        if (Status == NodeStatus.Inactive)
        {
            throw new InvalidOperationException("University is already inactive.");
        }

        Status = NodeStatus.Inactive;
    }

    public void Activate()
    {
        if (Status == NodeStatus.Active)
        {
            throw new InvalidOperationException("University is already active.");
        }

        Status = NodeStatus.Active;
    }
}
