using Microsoft.EntityFrameworkCore;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.Infrastructure.Persistence.Repositories;

/// <summary>
/// design-decisions.md "Interview-Slot Booking Concurrency Control": <see cref="TryClaimSlotAsync"/>
/// is a single, atomic <c>UPDATE ... WHERE booked_count &lt; capacity</c> statement - mirrors Alumni's
/// own <c>MentorshipOptInRepository.TryClaimMentorCapacityAsync</c> exactly. <see cref="GetByIdAsync"/>
/// uses <c>AsNoTracking</c> so a pre-flight read here can never leave a stale tracked instance behind
/// once this same repository's raw-SQL write runs against the same row (ums-core-gotchas).
/// </summary>
internal sealed class InterviewSlotRepository(CareerDbContext context) : IInterviewSlotRepository
{
    public Task<InterviewSlot?> GetByIdAsync(InterviewSlotId id, CancellationToken cancellationToken = default) =>
        context.InterviewSlots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<InterviewSlot>> ListByDriveAsync(CampusRecruitmentDriveId driveId, CancellationToken cancellationToken = default) =>
        await context.InterviewSlots.AsNoTracking().Where(s => s.DriveId == driveId).OrderBy(s => s.StartTime)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void AddRange(IEnumerable<InterviewSlot> slots) => context.InterviewSlots.AddRange(slots);

    public async Task<bool> TryClaimSlotAsync(InterviewSlotId slotId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE career.interview_slots
             SET booked_count = booked_count + 1
             WHERE id = {slotId.Value} AND is_cancelled = false AND booked_count < capacity
             """,
            cancellationToken).ConfigureAwait(false);

        return affected == 1;
    }

    public async Task ReleaseSlotAsync(InterviewSlotId slotId, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE career.interview_slots
             SET booked_count = GREATEST(booked_count - 1, 0)
             WHERE id = {slotId.Value}
             """,
            cancellationToken).ConfigureAwait(false);
    }
}
