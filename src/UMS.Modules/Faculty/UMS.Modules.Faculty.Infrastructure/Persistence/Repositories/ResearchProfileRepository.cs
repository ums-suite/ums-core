using Microsoft.EntityFrameworkCore;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.ResearchProfiles;

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Repositories;

internal sealed class ResearchProfileRepository(FacultyDbContext context) : IResearchProfileRepository
{
    public Task<ResearchProfile?> GetByFacultyMemberIdAsync(Guid facultyMemberId, CancellationToken cancellationToken = default) =>
        context.ResearchProfiles.Include(r => r.Publications).FirstOrDefaultAsync(r => r.FacultyMemberId == facultyMemberId, cancellationToken);

    public void Add(ResearchProfile researchProfile) => context.ResearchProfiles.Add(researchProfile);
}
