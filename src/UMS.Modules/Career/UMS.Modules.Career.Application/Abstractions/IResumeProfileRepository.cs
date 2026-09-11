using UMS.Modules.Career.Domain.ResumeProfiles;

namespace UMS.Modules.Career.Application.Abstractions;

public interface IResumeProfileRepository
{
    public Task<ResumeProfile?> GetByIdAsync(ResumeProfileId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<ResumeProfile>> ListByStudentAsync(Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §2.7: exactly one `ResumeProfile` per Student is flagged default at any time - used to un-flag the previous default before flagging a new one.</summary>
    public Task<ResumeProfile?> GetDefaultAsync(Guid studentId, CancellationToken cancellationToken = default);

    public void Add(ResumeProfile profile);
}
