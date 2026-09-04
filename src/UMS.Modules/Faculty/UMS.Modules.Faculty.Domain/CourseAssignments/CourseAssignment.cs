using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Domain.CourseAssignments;

/// <summary>
/// FAC-4: a Faculty-owned, eventually-consistent projection of Academic's own authoritative
/// instructor assignment (requirement-spec.md faculty §2 Course Assignment, §9's first Decision).
/// References <see cref="CourseOfferingId"/>/<see cref="FacultyMemberId"/> by id only - never a
/// cross-schema FK (§3).
///
/// <para>
/// The ordering guard on <see cref="ApplyAssigned"/>/<see cref="ApplyUnassigned"/>
/// (<see cref="LastAppliedEventOccurredAt"/>) is design-decisions.md's "Event Consumer Idempotency
/// and Ordering" decision, enforced on the aggregate itself rather than trusted to the caller: an
/// event whose <c>occurredAt</c> is not strictly after the last one actually applied is a no-op,
/// closing both edge-cases.md's duplicate-delivery and out-of-order-delivery cases with the same
/// check.
/// </para>
/// </summary>
public sealed class CourseAssignment : AggregateRoot<CourseAssignmentId>
{
    private CourseAssignment()
    {
    }

    private CourseAssignment(CourseAssignmentId id, Guid facultyMemberId, Guid courseOfferingId, DateTimeOffset eventOccurredAt)
    {
        Id = id;
        FacultyMemberId = facultyMemberId;
        CourseOfferingId = courseOfferingId;
        Status = CourseAssignmentStatus.Active;
        AssignedAt = eventOccurredAt;
        LastAppliedEventOccurredAt = eventOccurredAt;
    }

    public Guid FacultyMemberId { get; private set; }

    public Guid CourseOfferingId { get; private set; }

    public CourseAssignmentStatus Status { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>The <c>occurredAt</c> of the most recent inbound event actually applied - the monotonic ordering guard (see class remarks).</summary>
    public DateTimeOffset LastAppliedEventOccurredAt { get; private set; }

    public static CourseAssignment CreateFromAssigned(Guid facultyMemberId, Guid courseOfferingId, DateTimeOffset eventOccurredAt) =>
        new(CourseAssignmentId.New(), facultyMemberId, courseOfferingId, eventOccurredAt);

    /// <summary>Re-activates an existing (ended) projection row for the same pair - a reassignment after a prior unassign. Returns false (no-op) if <paramref name="eventOccurredAt"/> is stale.</summary>
    public bool ApplyAssigned(DateTimeOffset eventOccurredAt)
    {
        if (eventOccurredAt <= LastAppliedEventOccurredAt)
        {
            return false;
        }

        Status = CourseAssignmentStatus.Active;
        AssignedAt = eventOccurredAt;
        EndedAt = null;
        LastAppliedEventOccurredAt = eventOccurredAt;
        return true;
    }

    public bool ApplyUnassigned(DateTimeOffset eventOccurredAt)
    {
        if (eventOccurredAt <= LastAppliedEventOccurredAt)
        {
            return false;
        }

        Status = CourseAssignmentStatus.Ended;
        EndedAt = eventOccurredAt;
        LastAppliedEventOccurredAt = eventOccurredAt;
        return true;
    }
}
