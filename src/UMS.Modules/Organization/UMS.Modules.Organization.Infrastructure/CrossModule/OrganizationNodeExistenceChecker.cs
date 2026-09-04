using Microsoft.EntityFrameworkCore;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Modules.Organization.Domain.Universities;
using UMS.Modules.Organization.Infrastructure.Persistence;
using UMS.Shared.Organization;
using OrgProgramId = UMS.Modules.Organization.Domain.Programs.ProgramId;

namespace UMS.Modules.Organization.Infrastructure.CrossModule;

/// <summary>
/// The one real implementation of <see cref="IOrganizationNodeExistenceChecker"/> - closes the gap
/// release/DEVELOPMENT_PLAN.md's Flow #4/#5 notes both flagged ("Identity's own existence check
/// was a permissive stub ... until Organization lands"). Registered against the shared interface
/// here (Organization's own composition root); Identity's Infrastructure now resolves this same
/// interface instead of its former <c>StubOrganizationNodeExistenceChecker</c> (see
/// <c>UMS.Modules.Identity.Infrastructure.Organization.OrganizationNodeExistenceCheckerAdapter</c>).
/// </summary>
internal sealed class OrganizationNodeExistenceChecker(OrganizationDbContext context) : IOrganizationNodeExistenceChecker
{
    public async Task<bool> ExistsAsync(Guid organizationNodeId, CancellationToken cancellationToken = default)
    {
        // Checked in roughly ancestor-then-descendant order rather than five parallel
        // EXISTS-subquery clauses in one query - id spaces never collide (each is its own GUID
        // column), and Organization's tiny table sizes (organization §2's own scale reference)
        // make five sequential index lookups negligible next to the extra query complexity of one
        // combined statement.
        if (await context.Universities.AnyAsync(u => u.Id == new UniversityId(organizationNodeId), cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await context.Campuses.AnyAsync(c => c.Id == new CampusId(organizationNodeId), cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await context.Faculties.AnyAsync(f => f.Id == new FacultyId(organizationNodeId), cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        if (await context.Departments.AnyAsync(d => d.Id == new DepartmentId(organizationNodeId), cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        return await context.Programs.AnyAsync(p => p.Id == new OrgProgramId(organizationNodeId), cancellationToken).ConfigureAwait(false);
    }
}
