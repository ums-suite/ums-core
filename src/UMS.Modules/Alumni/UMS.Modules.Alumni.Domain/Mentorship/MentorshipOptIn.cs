using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Mentorship;

/// <summary>
/// ALM-11: a mentor/mentee opt-in profile (requirement-spec.md §2.5 first bullet). One row per
/// (PersonId, Role) - a mentor's <see cref="CapacityLimit"/>/<see cref="ActiveCount"/> pair is the
/// atomic-conditional-write target design-decisions.md "Mentor-Capacity Enforcement Mechanism"
/// requires (mirrors Hostel's/Library's own capacity-limited-resource family: an atomic
/// <c>UPDATE ... WHERE active_count &lt; capacity_limit</c>, never a held lock).
/// </summary>
public sealed class MentorshipOptIn : AggregateRoot<MentorshipOptInId>
{
    private MentorshipOptIn()
    {
    }

    private MentorshipOptIn(MentorshipOptInId id, Guid personId, MentorshipRole role, string expertiseAreas, int capacityLimit, string? availability, DateTimeOffset now)
    {
        Id = id;
        PersonId = personId;
        Role = role;
        ExpertiseAreas = expertiseAreas;
        CapacityLimit = capacityLimit;
        Availability = availability;
        IsActive = true;
        CreatedAt = now;
    }

    /// <summary>An Alumnus id for <see cref="MentorshipRole.Mentor"/>, a Student id for <see cref="MentorshipRole.Mentee"/>.</summary>
    public Guid PersonId { get; private set; }

    public MentorshipRole Role { get; private set; }

    public string ExpertiseAreas { get; private set; } = string.Empty;

    /// <summary>requirement-spec.md §2.5: "self-declared capacity limit" - meaningful only for <see cref="MentorshipRole.Mentor"/>.</summary>
    public int CapacityLimit { get; private set; }

    /// <summary>Both <c>Proposed</c> and <c>Active</c> matches count against capacity (design-decisions.md). Maintained ONLY via the atomic conditional-write repository method - never mutated directly here, so this in-memory value may be stale immediately after a concurrent write; it exists for read/reporting purposes only.</summary>
    public int ActiveCount { get; private set; }

    public string? Availability { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static MentorshipOptIn OptIn(Guid personId, MentorshipRole role, string expertiseAreas, int capacityLimit, string? availability, DateTimeOffset now)
    {
        if (capacityLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacityLimit), "A capacity limit cannot be negative.");
        }

        return new MentorshipOptIn(MentorshipOptInId.New(), personId, role, expertiseAreas?.Trim() ?? string.Empty, capacityLimit, availability?.Trim(), now);
    }

    public void Deactivate() => IsActive = false;
}
