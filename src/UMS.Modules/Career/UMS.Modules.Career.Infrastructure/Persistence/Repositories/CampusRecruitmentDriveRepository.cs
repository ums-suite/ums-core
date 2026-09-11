using Microsoft.EntityFrameworkCore;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.Infrastructure.Persistence.Repositories;

internal sealed class CampusRecruitmentDriveRepository(CareerDbContext context) : ICampusRecruitmentDriveRepository
{
    public Task<CampusRecruitmentDrive?> GetByIdAsync(CampusRecruitmentDriveId id, CancellationToken cancellationToken = default) =>
        context.Drives.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CampusRecruitmentDrive>> ListAsync(Guid? employerProfileId, bool includeAllStatuses, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Drives.AsQueryable();

        if (!includeAllStatuses)
        {
            query = query.Where(d => d.Status == DriveStatus.Scheduled || d.Status == DriveStatus.RegistrationOpen);
        }

        if (employerProfileId is { } employer)
        {
            query = query.Where(d => d.EmployerProfileId == employer);
        }

        return await query.OrderByDescending(d => d.ScheduledDate).Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Add(CampusRecruitmentDrive drive) => context.Drives.Add(drive);
}
