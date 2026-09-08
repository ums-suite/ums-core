using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Shared.Academic;
using UMS.Shared.Faculty;

namespace UMS.Modules.Reporting.Application.DashboardMetrics;

/// <summary>
/// RPT-7: the Faculty dashboard spans TWO source modules - Faculty owns employment-side figures
/// (headcount/status, leave) while Academic owns teaching-load/attendance/grade-distribution/
/// course-completion (resolved via each CourseOffering's own Faculty↔CourseOffering linkage - see
/// <see cref="IAcademicReportingQuery.GetTeachingAggregatesAsync"/>'s own remarks). This job calls
/// both and merges them into one combined payload, nightly (requirement-spec.md §2.2 Faculty row).
/// </summary>
public sealed class FacultyDashboardRefreshService(
    IFacultyReportingQuery facultyQuery,
    IAcademicReportingQuery academicQuery,
    IMetricRefreshLease lease,
    IDashboardMetricRepository metrics,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<FacultyDashboardRefreshService> logger)
    : MetricRefreshJobBase(lease, metrics, unitOfWork, clock, logger)
{
    public const string MetricKeyValue = "faculty-dashboard";

    protected override string MetricKey => MetricKeyValue;

    protected override string DisplayName => "Faculty Dashboard";

    protected override TimeSpan LeaseTtl => TimeSpan.FromMinutes(30);

    protected override async Task<string> ComputePayloadJsonAsync(CancellationToken cancellationToken)
    {
        var facultySnapshot = await facultyQuery.GetDashboardSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var teachingSnapshot = await academicQuery.GetTeachingAggregatesAsync(cancellationToken).ConfigureAwait(false);

        var payload = new FacultyDashboardPayload(
            facultySnapshot.TotalFacultyMembers,
            facultySnapshot.FacultyByStatus,
            facultySnapshot.PendingLeaveRequests,
            facultySnapshot.ApprovedLeaveRequestsThisMonth,
            facultySnapshot.AverageApprovedLeaveDurationDays,
            teachingSnapshot.TeachingLoadByFacultyMember,
            teachingSnapshot.OverallAttendanceRate,
            teachingSnapshot.GradeDistribution,
            teachingSnapshot.CourseCompletionRate);

        return JsonSerializer.Serialize(payload);
    }
}

/// <summary>The Faculty dashboard's own combined read model - Faculty-owned fields first, then the Academic-owned teaching-side fields (see class remarks on this dashboard's own the source-module split).</summary>
public sealed record FacultyDashboardPayload(
    int TotalFacultyMembers,
    IReadOnlyDictionary<string, int> FacultyByStatus,
    int PendingLeaveRequests,
    int ApprovedLeaveRequestsThisMonth,
    decimal AverageApprovedLeaveDurationDays,
    IReadOnlyDictionary<Guid, int> TeachingLoadByFacultyMember,
    decimal OverallAttendanceRate,
    IReadOnlyDictionary<string, int> GradeDistribution,
    decimal CourseCompletionRate);
