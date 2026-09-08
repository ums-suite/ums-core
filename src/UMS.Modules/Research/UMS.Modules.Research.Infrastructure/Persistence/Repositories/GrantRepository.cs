using Microsoft.EntityFrameworkCore;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.Grants;

namespace UMS.Modules.Research.Infrastructure.Persistence.Repositories;

internal sealed class GrantRepository(ResearchDbContext context) : IGrantRepository
{
    public Task<Grant?> GetByIdAsync(GrantId id, CancellationToken cancellationToken = default) =>
        context.Grants.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public Task<GrantStatus?> GetStatusAsync(Guid grantId, CancellationToken cancellationToken = default) =>
        context.Grants.Where(g => g.Id == new GrantId(grantId)).Select(g => (GrantStatus?)g.Status).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Grant>> ListAsync(Guid? principalInvestigatorFacultyMemberId, Guid? facultyMemberId, GrantStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = context.Grants.AsQueryable();

        if (principalInvestigatorFacultyMemberId is { } piId)
        {
            query = query.Where(g => g.Investigators.Any(i => i.FacultyMemberId == piId && i.Role == GrantInvestigatorRole.PrincipalInvestigator));
        }

        if (facultyMemberId is { } fmId)
        {
            query = query.Where(g => g.Investigators.Any(i => i.FacultyMemberId == fmId));
        }

        if (status is { } s)
        {
            query = query.Where(g => g.Status == s);
        }

        return await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Grant>> ListPubliclyVisibleAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Grants
            .Where(g => g.IsPubliclyVisible)
            .OrderByDescending(g => g.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Grant>> ListActiveWherePrincipalInvestigatorAsync(Guid facultyMemberId, CancellationToken cancellationToken = default) =>
        await context.Grants
            .Where(g => (g.Status == GrantStatus.Funded || g.Status == GrantStatus.Active)
                && g.Investigators.Any(i => i.FacultyMemberId == facultyMemberId && i.Role == GrantInvestigatorRole.PrincipalInvestigator))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Grant grant) => context.Grants.Add(grant);
}
