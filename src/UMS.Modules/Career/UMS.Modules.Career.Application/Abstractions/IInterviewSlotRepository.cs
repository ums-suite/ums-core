using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.Application.Abstractions;

/// <summary>
/// design-decisions.md "Interview-Slot Booking Concurrency Control": the sole mutation path for a
/// slot's <c>booked_count</c> is <see cref="TryClaimSlotAsync"/>'s atomic conditional
/// <c>UPDATE ... WHERE booked_count &lt; capacity</c> - mirrors Alumni's own
/// <c>IMentorshipOptInRepository.TryClaimMentorCapacityAsync</c> exactly (a single
/// <c>ExecuteSqlInterpolatedAsync</c> statement suffices; no multi-step lock-then-read is needed,
/// since <see cref="InterviewSlot"/>'s capacity columns are plain scalars, never a
/// <c>ComplexProperty</c>-mapped value object).
/// </summary>
public interface IInterviewSlotRepository
{
    public Task<InterviewSlot?> GetByIdAsync(InterviewSlotId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<InterviewSlot>> ListByDriveAsync(CampusRecruitmentDriveId driveId, CancellationToken cancellationToken = default);

    public void AddRange(IEnumerable<InterviewSlot> slots);

    /// <summary>Returns <see langword="true"/> only if the conditional UPDATE actually affected a row (a slot was genuinely claimed).</summary>
    public Task<bool> TryClaimSlotAsync(InterviewSlotId slotId, CancellationToken cancellationToken = default);

    /// <summary>edge-cases.md "A Student withdraws a booked InterviewSlot shortly before the drive" - releases atomically, immediately making the slot bookable again.</summary>
    public Task ReleaseSlotAsync(InterviewSlotId slotId, CancellationToken cancellationToken = default);
}
