using Microsoft.EntityFrameworkCore;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.ResumeProfiles;

namespace UMS.Modules.Career.Infrastructure.Persistence.Repositories;

internal sealed class ResumeProfileRepository(CareerDbContext context) : IResumeProfileRepository
{
    public Task<ResumeProfile?> GetByIdAsync(ResumeProfileId id, CancellationToken cancellationToken = default) =>
        context.ResumeProfiles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ResumeProfile>> ListByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        await context.ResumeProfiles
            .Where(r => r.StudentId == studentId && !r.IsDeleted)
            .OrderByDescending(r => r.IsDefault)
            .ThenByDescending(r => r.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<ResumeProfile?> GetDefaultAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        context.ResumeProfiles.FirstOrDefaultAsync(r => r.StudentId == studentId && r.IsDefault && !r.IsDeleted, cancellationToken);

    public void Add(ResumeProfile profile) => context.ResumeProfiles.Add(profile);
}
