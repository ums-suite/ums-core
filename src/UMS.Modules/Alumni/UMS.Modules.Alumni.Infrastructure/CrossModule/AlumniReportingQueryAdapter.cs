using Microsoft.EntityFrameworkCore;
using UMS.Modules.Alumni.Domain.Donations;
using UMS.Modules.Alumni.Domain.Jobs;
using UMS.Modules.Alumni.Domain.Mentorship;
using UMS.Modules.Alumni.Infrastructure.Persistence;
using UMS.Shared.Alumni;

namespace UMS.Modules.Alumni.Infrastructure.CrossModule;

/// <summary>
/// Flow #31: the one real implementation of <see cref="IAlumniReportingQuery"/> - mirrors
/// <c>UMS.Modules.Content.Infrastructure.CrossModule.ContentReportingQueryAdapter</c>'s exact pattern
/// (Flow #26's own precedent). See the shared interface's own remarks for why this is a deliberate
/// scope extension (an eighth, Alumni-owned admin dashboard) rather than an item named in Reporting's
/// original six-dashboard list.
/// </summary>
internal sealed class AlumniReportingQueryAdapter(AlumniDbContext context) : IAlumniReportingQuery
{
    public async Task<AlumniDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalAlumnusCount = await context.Alumni.CountAsync(cancellationToken).ConfigureAwait(false);

        // design-decisions.md "Donation Confirmation Consistency Model": only Confirmed rows ever
        // contribute - a Pending or Failed Donation is never counted toward the total, mirroring
        // UMS.Shared.Research.IResearchReportingQuery's own confirmed-funding-by-currency shape.
        var confirmedDonationAmountByCurrency = await context.Donations
            .Where(d => d.Status == DonationStatus.Confirmed)
            .GroupBy(d => d.Currency)
            .Select(g => new { Currency = g.Key, Total = g.Sum(d => d.Amount) })
            .ToDictionaryAsync(x => x.Currency, x => x.Total, cancellationToken)
            .ConfigureAwait(false);

        // JobPosting.Status already IS the current lifecycle state - ALM-6's scheduled expiry sweep,
        // never a live ad hoc expires_at recheck, is what keeps Published/Expired current.
        var activeJobPostingCount = await context.JobPostings
            .CountAsync(j => j.Status == JobPostingStatus.Published, cancellationToken)
            .ConfigureAwait(false);

        // requirement-spec.md §2.5/§4: Active means both MentorAcceptedAt/MenteeAcceptedAt are set -
        // read from the entity's own already-computed Status, never re-derived here.
        var activeMentorshipMatchCount = await context.MentorshipMatches
            .CountAsync(m => m.Status == MentorshipMatchStatus.Active, cancellationToken)
            .ConfigureAwait(false);

        return new AlumniDashboardSnapshot(totalAlumnusCount, confirmedDonationAmountByCurrency, activeJobPostingCount, activeMentorshipMatchCount);
    }
}
