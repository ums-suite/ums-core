using Microsoft.EntityFrameworkCore;
using UMS.Modules.Faculty.Domain.LeaveRequests;
using UMS.Modules.Faculty.Infrastructure.Persistence;
using UMS.Shared.Faculty;

namespace UMS.Modules.Faculty.Infrastructure.CrossModule;

/// <summary>
/// RPT-3/RPT-7: the one real implementation of <see cref="IFacultyReportingQuery"/> - mirrors
/// <c>UMS.Modules.Academic.Infrastructure.CrossModule.AcademicReportingQueryAdapter</c>'s exact
/// pattern. Scoped to Faculty-owned data only - see the shared interface's own remarks on why
/// teaching-load/attendance/grade/course-completion figures live on
/// <c>UMS.Shared.Academic.IAcademicReportingQuery</c> instead.
/// </summary>
internal sealed class FacultyReportingQueryAdapter(FacultyDbContext context) : IFacultyReportingQuery
{
    public async Task<FacultyDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalFacultyMembers = await context.FacultyMembers.CountAsync(cancellationToken).ConfigureAwait(false);

        var facultyByStatus = await context.FacultyMembers
            .GroupBy(f => f.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Status.ToString(), g => g.Count, cancellationToken).ConfigureAwait(false);

        var pendingLeaveRequests = await context.LeaveRequests
            .CountAsync(l => l.Status == LeaveRequestStatus.Submitted || l.Status == LeaveRequestStatus.DeptHeadApproved, cancellationToken)
            .ConfigureAwait(false);

        var utcNow = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(utcNow.Year, utcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var approvedThisMonth = await context.LeaveRequests
            .Where(l => l.Status == LeaveRequestStatus.Approved && l.DecidedAt != null && l.DecidedAt >= monthStart)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var approvedDurations = await context.LeaveRequests
            .Where(l => l.Status == LeaveRequestStatus.Approved)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var averageDuration = approvedDurations.Count > 0
            ? Math.Round((decimal)approvedDurations.Average(l => (l.EndDate.DayNumber - l.StartDate.DayNumber) + 1), 1)
            : 0m;

        return new FacultyDashboardSnapshot(totalFacultyMembers, facultyByStatus, pendingLeaveRequests, approvedThisMonth.Count, averageDuration);
    }
}
