using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Domain.Facilities;
using UMS.Modules.Organization.Infrastructure.Persistence;
using UMS.Shared.Organization;

namespace UMS.Modules.Organization.Infrastructure.CrossModule;

/// <summary>
/// The one real implementation of <see cref="IRoomExistenceChecker"/> - Career (Flow #30) is this
/// contract's first real caller, resolving a `CampusRecruitmentDrive`'s venue reference without a
/// competing Career-owned venue concept (career requirement-spec.md §2.3/§7). Mirrors
/// <see cref="OrganizationNodeExistenceChecker"/>'s own exact pattern.
/// </summary>
internal sealed class RoomExistenceChecker(OrganizationDbContext context) : IRoomExistenceChecker
{
    public Task<bool> ExistsAsync(Guid roomId, CancellationToken cancellationToken = default) =>
        context.Rooms.AnyAsync(r => r.Id == new RoomId(roomId), cancellationToken);
}
