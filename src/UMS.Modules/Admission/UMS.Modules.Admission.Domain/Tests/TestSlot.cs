namespace UMS.Modules.Admission.Domain.Tests;

/// <summary>
/// edge-cases.md "Test-slot capacity race at admit-card generation": <see cref="RemainingSeats"/>
/// is NEVER mutated through this entity's own in-memory setter/SaveChanges path when an admit card
/// is actually assigned - design-decisions.md's chosen mechanism is an atomic conditional
/// <c>UPDATE test_slots SET remaining_seats = remaining_seats - 1 WHERE id = @id AND
/// remaining_seats > 0</c>, issued directly by <c>AdmissionTestRepository.TryClaimSlotSeatAsync</c>,
/// mirroring <c>CourseOffering.EnrolledCount</c>'s own identical pattern exactly. The field exists
/// on this entity for reads (capacity displays) and construction-time validation only.
/// </summary>
public sealed class TestSlot
{
    internal TestSlot(TestSlotId id, DateTimeOffset startAt, DateTimeOffset endAt, int capacity)
    {
        Id = id;
        StartAt = startAt;
        EndAt = endAt;
        Capacity = capacity;
        RemainingSeats = capacity;
    }

    private TestSlot()
    {
    }

    public TestSlotId Id { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public int Capacity { get; private set; }

    /// <summary>See class remarks - read-only from this entity's own perspective.</summary>
    public int RemainingSeats { get; private set; }
}
