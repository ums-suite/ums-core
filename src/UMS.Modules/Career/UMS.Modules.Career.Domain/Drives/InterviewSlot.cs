namespace UMS.Modules.Career.Domain.Drives;

/// <summary>
/// CAR-12/CAR-13: the atomic bookable unit (requirement-spec.md §2.3, §3 - module-local term pending
/// glossary merge), a child of <see cref="CampusRecruitmentDrive"/>.
///
/// <para>
/// ums-core-gotchas: deliberately mapped as its OWN table with a normal one-to-many FK relationship
/// (never an EF <c>OwnsMany</c> owned collection) - sidesteps the documented EF Core 10.0.11
/// "misfires UPDATE instead of INSERT for a brand-new owned-collection child row keyed on a
/// caller-supplied business value, on an already-persisted parent" gotcha entirely, the same shape
/// Hostel's own <c>Bed</c> (a child of <c>Room</c>) already resolved by NOT using an owned collection.
/// </para>
///
/// <para>
/// design-decisions.md "Interview-Slot Booking Concurrency Control": <see cref="BookedCount"/> is
/// mutated ONLY via <c>IInterviewSlotRepository</c>'s atomic conditional
/// <c>UPDATE ... WHERE booked_count &lt; capacity</c> (mirrors Alumni's own
/// <c>MentorshipOptInRepository.TryClaimMentorCapacityAsync</c> exactly) - never through this class's
/// own setters from ordinary application code, which is why they stay <see langword="private"/> with
/// no public mutator beyond construction. A DB-level check constraint
/// (<c>booked_count &lt;= capacity</c>) backs this as defense-in-depth.
/// </para>
/// </summary>
public sealed class InterviewSlot
{
    private InterviewSlot()
    {
    }

    private InterviewSlot(InterviewSlotId id, CampusRecruitmentDriveId driveId, DateTimeOffset startTime, DateTimeOffset endTime, int capacity)
    {
        Id = id;
        DriveId = driveId;
        StartTime = startTime;
        EndTime = endTime;
        Capacity = capacity;
        BookedCount = 0;
    }

    public InterviewSlotId Id { get; private set; }

    public CampusRecruitmentDriveId DriveId { get; private set; }

    public DateTimeOffset StartTime { get; private set; }

    public DateTimeOffset EndTime { get; private set; }

    /// <summary>requirement-spec.md §2.3: default capacity 1.</summary>
    public int Capacity { get; private set; }

    /// <summary>Mutated ONLY by <c>IInterviewSlotRepository</c>'s atomic conditional UPDATE - see this class's own remarks.</summary>
    public int BookedCount { get; private set; }

    public bool IsCancelled { get; private set; }

    public static InterviewSlot Create(CampusRecruitmentDriveId driveId, DateTimeOffset startTime, DateTimeOffset endTime, int capacity)
    {
        if (endTime <= startTime)
        {
            throw new ArgumentException("An InterviewSlot's end time must be after its start time.", nameof(endTime));
        }

        if (capacity < 1)
        {
            throw new ArgumentException("An InterviewSlot's capacity must be at least 1.", nameof(capacity));
        }

        return new InterviewSlot(InterviewSlotId.New(), driveId, startTime, endTime, capacity);
    }

    public bool HasCapacity() => !IsCancelled && BookedCount < Capacity;

    public void MarkCancelled() => IsCancelled = true;
}
