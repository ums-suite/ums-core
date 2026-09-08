namespace UMS.Shared.Faculty;

/// <summary>
/// RPT-3/RPT-7: mirrors <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s exact pattern - lives
/// here (not <c>UMS.Modules.Faculty.*</c>) so Reporting can call it without a forbidden dependency
/// on Faculty's internals (module-boundaries.md, ADR-0002). Faculty's own Infrastructure layer
/// registers the one real implementation.
///
/// <para>
/// Deliberately scoped to Faculty-OWNED data only - employment-side aggregates (FacultyMember
/// headcount/status, LeaveRequest figures). Teaching-load, attendance, grade-distribution, and
/// course-completion figures the reporting requirement-spec.md §2.2 Faculty row also names are
/// actually Academic-owned data (Grade/Attendance/Enrollment/CourseOffering), resolved via
/// <c>UMS.Shared.Academic.IAcademicReportingQuery.GetTeachingAggregatesAsync</c> instead - see that
/// method's own remarks for why the Faculty dashboard is not self-contained inside Faculty's own
/// schema. No <c>asOf</c> parameter - see <c>IAcademicReportingQuery</c>'s own remarks for the
/// documented read-consistency simplification.
/// </para>
/// </summary>
public interface IFacultyReportingQuery
{
    /// <summary>RPT-7: FacultyMember headcount/status breakdown and LeaveRequest figures - the Faculty-owned half of the Faculty dashboard.</summary>
    public Task<FacultyDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

public sealed record FacultyDashboardSnapshot(
    int TotalFacultyMembers,
    IReadOnlyDictionary<string, int> FacultyByStatus,
    int PendingLeaveRequests,
    int ApprovedLeaveRequestsThisMonth,
    decimal AverageApprovedLeaveDurationDays);
