using UMS.Modules.Faculty.Domain.ResearchProfiles;

namespace UMS.Modules.Faculty.Application.Abstractions;

public interface IResearchProfileRepository
{
    public Task<ResearchProfile?> GetByFacultyMemberIdAsync(Guid facultyMemberId, CancellationToken cancellationToken = default);

    public void Add(ResearchProfile researchProfile);
}
