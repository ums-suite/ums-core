using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Mentorship;

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Repositories;

internal sealed class MentorshipMatchRepository(AlumniDbContext context) : IMentorshipMatchRepository
{
    public Task<MentorshipMatch?> GetByIdAsync(MentorshipMatchId id, CancellationToken cancellationToken = default) =>
        context.MentorshipMatches.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MentorshipMatch>> ListByMentorAsync(Guid mentorAlumnusId, CancellationToken cancellationToken = default) =>
        await context.MentorshipMatches.Where(m => m.MentorAlumnusId == mentorAlumnusId).OrderByDescending(m => m.ProposedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<MentorshipMatch>> ListByMenteeAsync(Guid menteeStudentId, CancellationToken cancellationToken = default) =>
        await context.MentorshipMatches.Where(m => m.MenteeStudentId == menteeStudentId).OrderByDescending(m => m.ProposedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(MentorshipMatch match) => context.MentorshipMatches.Add(match);
}
