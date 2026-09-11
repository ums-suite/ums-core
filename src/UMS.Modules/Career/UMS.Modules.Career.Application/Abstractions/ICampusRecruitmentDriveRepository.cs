using UMS.Modules.Career.Domain.Drives;

namespace UMS.Modules.Career.Application.Abstractions;

public interface ICampusRecruitmentDriveRepository
{
    public Task<CampusRecruitmentDrive?> GetByIdAsync(CampusRecruitmentDriveId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CampusRecruitmentDrive>> ListAsync(Guid? employerProfileId, bool includeAllStatuses, int skip, int take, CancellationToken cancellationToken = default);

    public void Add(CampusRecruitmentDrive drive);
}
