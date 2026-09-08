using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Infrastructure.Persistence;
using UMS.Shared.Hostel;

namespace UMS.Modules.Hostel.Infrastructure.CrossModule;

/// <summary>
/// RPT-3/RPT-8: the one real implementation of <see cref="IHostelReportingQuery"/> - mirrors
/// <c>UMS.Modules.Academic.Infrastructure.CrossModule.AcademicReportingQueryAdapter</c>'s exact
/// pattern. Occupancy is derived entirely from <c>Allocation</c>'s own <c>Active</c>-status rows
/// against the Bed count (see <c>Bed</c>'s own remarks: "availability is derived entirely from
/// hostel.allocations ... never a denormalized status field") - exactly the same derivation
/// Hostel's own live availability check uses, just aggregated here instead of per-bed.
/// </summary>
internal sealed class HostelReportingQueryAdapter(HostelDbContext context) : IHostelReportingQuery
{
    public async Task<HostelDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalBeds = await context.Beds.CountAsync(cancellationToken).ConfigureAwait(false);

        var occupiedBeds = await context.Allocations
            .Where(a => a.Status == AllocationStatus.Active)
            .Select(a => a.BedId)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var availableBeds = Math.Max(totalBeds - occupiedBeds, 0);
        var occupancyPercentage = totalBeds > 0 ? Math.Round((decimal)occupiedBeds / totalBeds * 100m, 2) : 0m;

        var pendingApplications = await context.HostelApplications
            .CountAsync(a => a.Status == HostelApplicationStatus.Submitted || a.Status == HostelApplicationStatus.Ranked, cancellationToken)
            .ConfigureAwait(false);

        // requirement-spec.md §2.2's "hostel fees" figure, reported as a COUNT of Allocations by
        // fee status rather than a currency amount - see IHostelReportingQuery's own remarks on why
        // (Hostel's own schema tracks fee status, not the fee's amount, which lives in Finance).
        var feePaidCount = await context.Allocations
            .CountAsync(a => a.Status == AllocationStatus.FeePaid || a.Status == AllocationStatus.Active || a.Status == AllocationStatus.CheckedOut, cancellationToken)
            .ConfigureAwait(false);
        var feePendingCount = await context.Allocations.CountAsync(a => a.Status == AllocationStatus.Pending, cancellationToken).ConfigureAwait(false);

        return new HostelDashboardSnapshot(totalBeds, occupiedBeds, availableBeds, occupancyPercentage, pendingApplications, feePaidCount, feePendingCount);
    }
}
