using UMS.Modules.Alumni.Domain.Mentorship;

namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IMentorshipMatchRepository
{
    public Task<MentorshipMatch?> GetByIdAsync(MentorshipMatchId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<MentorshipMatch>> ListByMentorAsync(Guid mentorAlumnusId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<MentorshipMatch>> ListByMenteeAsync(Guid menteeStudentId, CancellationToken cancellationToken = default);

    public void Add(MentorshipMatch match);
}
