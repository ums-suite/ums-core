using Microsoft.EntityFrameworkCore;
using UMS.Modules.Research.Domain.Grants;
using UMS.Modules.Research.Infrastructure.Persistence;
using UMS.Shared.Research;

namespace UMS.Modules.Research.Infrastructure.CrossModule;

/// <summary>
/// Flow #26: the one real implementation of <see cref="IResearchReportingQuery"/> - mirrors
/// <c>UMS.Modules.Faculty.Infrastructure.CrossModule.FacultyReportingQueryAdapter</c>'s exact
/// pattern. See the shared interface's own remarks for why this replaces the Flow #22 seeder's
/// Faculty-headcount proxy for the "Research" regulatory-report category.
/// </summary>
internal sealed class ResearchReportingQueryAdapter(ResearchDbContext context) : IResearchReportingQuery
{
    /// <summary>requirement-spec.md §2 Grant Lifecycle: a <c>Proposed</c> Grant's funding amount is not yet awarded, and a <c>Rejected</c>/<c>Withdrawn</c> Grant's never was - only these four statuses represent a real, confirmed funding commitment.</summary>
    private static readonly GrantStatus[] ConfirmedFundingStatuses =
        [GrantStatus.Funded, GrantStatus.Active, GrantStatus.Closed, GrantStatus.Reported];

    public async Task<ResearchDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalGrants = await context.Grants.CountAsync(cancellationToken).ConfigureAwait(false);
        var totalActiveGrants = await context.Grants.CountAsync(g => g.Status == GrantStatus.Active, cancellationToken).ConfigureAwait(false);

        // Materialized client-side before grouping (mirrors Faculty's own
        // FacultyReportingQueryAdapter posture) - Money's Amount/Currency are read via a
        // ComplexProperty projection here, never grouped server-side, to avoid relying on the EF
        // provider's own complex-type GroupBy translation support.
        var confirmedFundingAmounts = await context.Grants
            .Where(g => ConfirmedFundingStatuses.Contains(g.Status))
            .Select(g => new { g.FundingAmount.Amount, g.FundingAmount.Currency })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var confirmedFundingAmountByCurrency = confirmedFundingAmounts
            .GroupBy(g => g.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        // A merged (duplicate-losing) Publication row is never counted - it is no longer the
        // canonical record of that work (design-decisions.md's duplicate-merge decision).
        var totalPublications = await context.Publications
            .CountAsync(p => p.MergedIntoPublicationId == null, cancellationToken).ConfigureAwait(false);

        var totalRepositoryEntries = await context.InstitutionalRepositoryEntries.CountAsync(cancellationToken).ConfigureAwait(false);

        return new ResearchDashboardSnapshot(totalGrants, totalActiveGrants, confirmedFundingAmountByCurrency, totalPublications, totalRepositoryEntries);
    }
}
