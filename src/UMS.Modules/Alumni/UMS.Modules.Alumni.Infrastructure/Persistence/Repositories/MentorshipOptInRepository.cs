using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Mentorship;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

/// <summary>
/// design-decisions.md "Mentor-Capacity Enforcement Mechanism": <see cref="TryClaimMentorCapacityAsync"/>
/// is a single, atomic <c>UPDATE ... WHERE active_count &lt; capacity_limit</c> statement (mirrors
/// the lock-then-conditional-write FAMILY Hostel's bed allocation/Library's copy issuance use, but as
/// a single statement rather than a separate lock query - MentorshipOptIn's capacity columns are
/// plain scalars with no ComplexProperty/spanning index, so the ums-core-gotchas two-step
/// "raw lock, discard, then tracked LINQ" workaround is not needed here; a plain
/// <c>ExecuteSqlInterpolatedAsync</c> conditional UPDATE already closes the race on its own).
/// </summary>
internal sealed class MentorshipOptInRepository(AlumniDbContext context) : IMentorshipOptInRepository
{
    public Task<MentorshipOptIn?> GetAsync(Guid personId, MentorshipRole role, CancellationToken cancellationToken = default) =>
        context.MentorshipOptIns.FirstOrDefaultAsync(o => o.PersonId == personId && o.Role == role, cancellationToken);

    public async Task<bool> TryClaimMentorCapacityAsync(Guid mentorAlumnusId, CancellationToken cancellationToken = default)
    {
        var affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE alumni.mentorship_opt_ins
             SET active_count = active_count + 1
             WHERE person_id = {mentorAlumnusId} AND role = 'Mentor' AND is_active = true AND active_count < capacity_limit
             """,
            cancellationToken).ConfigureAwait(false);

        return affected == 1;
    }

    public async Task ReleaseMentorCapacityAsync(Guid mentorAlumnusId, CancellationToken cancellationToken = default)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE alumni.mentorship_opt_ins
             SET active_count = GREATEST(active_count - 1, 0)
             WHERE person_id = {mentorAlumnusId} AND role = 'Mentor'
             """,
            cancellationToken).ConfigureAwait(false);
    }

    public void Add(MentorshipOptIn optIn) => context.MentorshipOptIns.Add(optIn);
}
