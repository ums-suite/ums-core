using Microsoft.EntityFrameworkCore;
using UMS.Modules.Career.Domain.Internships;
using UMS.Modules.Career.Infrastructure.Persistence;
using UMS.Shared.Career;

namespace UMS.Modules.Career.Infrastructure.CrossModule;

/// <summary>
/// Flow #31: the one real implementation of <see cref="ICareerReportingQuery"/> - mirrors
/// <c>UMS.Modules.Content.Infrastructure.CrossModule.ContentReportingQueryAdapter</c>'s exact pattern
/// (Flow #26's own precedent). See the shared interface's own remarks for why this is a deliberate
/// scope extension (a ninth, Career-owned admin dashboard) rather than an item named in Reporting's
/// original six-dashboard list.
/// </summary>
internal sealed class CareerReportingQueryAdapter(CareerDbContext context) : ICareerReportingQuery
{
    public async Task<CareerDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalInternshipCount = await context.Internships.CountAsync(cancellationToken).ConfigureAwait(false);

        // Mirrors Internship.IsBrowsable() exactly - Published OR ApplicationsOpen, never re-derived
        // from ApplicationDeadline here (CAR-3's scheduled sweep is what keeps status current).
        var publishedInternshipCount = await context.Internships
            .CountAsync(i => i.Status == InternshipStatus.Published || i.Status == InternshipStatus.ApplicationsOpen, cancellationToken)
            .ConfigureAwait(false);

        var totalCampusRecruitmentDriveCount = await context.Drives.CountAsync(cancellationToken).ConfigureAwait(false);

        var totalCareerApplicationCount = await context.CareerApplications.CountAsync(cancellationToken).ConfigureAwait(false);

        // design-decisions.md "Interview-Slot Booking Concurrency Control": BookedCount is mutated
        // ONLY via the atomic conditional-write repository method - summed here as-is, never
        // recomputed from a live join against CareerApplication.InterviewSlotId.
        var totalInterviewSlotBookings = await context.InterviewSlots
            .SumAsync(s => s.BookedCount, cancellationToken)
            .ConfigureAwait(false);

        return new CareerDashboardSnapshot(totalInternshipCount, publishedInternshipCount, totalCampusRecruitmentDriveCount, totalCareerApplicationCount, totalInterviewSlotBookings);
    }
}
