using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Domain.Attendance;
using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Modules.Academic.Infrastructure.Persistence;
using UMS.Shared.Academic;

namespace UMS.Modules.Academic.Infrastructure.CrossModule;

/// <summary>
/// RPT-3/RPT-4/RPT-7: the one real implementation of <see cref="IAcademicReportingQuery"/> - the
/// first outward-facing contract Academic exposes purely for Reporting's own scheduled pull (every
/// other cross-module contract this module already exposes,
/// <see cref="ICourseOfferingLookup"/>, is a live-operational lookup another transactional module
/// calls; this one is read-only, aggregate-only, and has exactly one caller class in the platform -
/// Reporting's <c>MetricRefreshJob</c>s).
///
/// <para>
/// Every aggregate here is computed with server-side <c>GROUP BY</c>/<c>COUNT</c> queries against
/// Academic's own <see cref="AcademicDbContext"/> - never a raw row materialized out to Reporting
/// (requirement-spec.md §4's "never a live cross-module join" - the JOIN/aggregation SQL runs
/// entirely inside this module's own Infrastructure class).
/// </para>
///
/// <para>
/// <b>Documented approximations</b> (first-pass, named rather than silently narrowed):
/// <see cref="AcademicDashboardSnapshot.NewEnrollmentsThisSession"/> uses a rolling 180-day
/// <c>CreatedAt</c> window as a proxy for "current academic session" (Academic exposes no single
/// "current session" concept easy to resolve without a further AcademicSession-shape lookup this
/// base flow's own aggregate queries don't need elsewhere); <see cref="AcademicDashboardSnapshot.GraduatingStudents"/>
/// counts distinct Students holding a <c>Completed</c> Enrollment - Academic's own schema has no
/// separate graduation-conferral record.
/// </para>
/// </summary>
internal sealed class AcademicReportingQueryAdapter(AcademicDbContext context) : IAcademicReportingQuery
{
    private static readonly TimeSpan CurrentSessionWindow = TimeSpan.FromDays(180);

    public async Task<AcademicDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var totalEnrollments = await context.Enrollments.CountAsync(cancellationToken).ConfigureAwait(false);
        var activeEnrollments = await context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Active, cancellationToken).ConfigureAwait(false);
        var droppedEnrollments = await context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Dropped, cancellationToken).ConfigureAwait(false);
        var completedEnrollments = await context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Completed, cancellationToken).ConfigureAwait(false);

        var sessionCutoff = DateTimeOffset.UtcNow - CurrentSessionWindow;
        var newEnrollmentsThisSession = await context.Enrollments.CountAsync(e => e.CreatedAt >= sessionCutoff, cancellationToken).ConfigureAwait(false);

        var gradeDistribution = await GetGradeDistributionAsync(cancellationToken).ConfigureAwait(false);

        var gradedCount = await context.Enrollments.CountAsync(e => e.Grade != null && e.Grade.LetterGrade != null, cancellationToken).ConfigureAwait(false);

        // Ordinal, non-culture letter-grade comparison ('F'/'f' are the only two failing-grade
        // spellings this codebase's Grade aggregate ever writes - see Grade.IsPassing's own ordinal
        // OrdinalIgnoreCase check) - written as a plain equality pair rather than a
        // case-conversion call so this predicate stays cleanly SQL-translatable (CA1304/CA1862
        // both flag ToUpper/ToUpperInvariant-based comparisons even inside an IQueryable
        // expression tree, where no CLR culture is actually involved).
        var passingCount = await context.Enrollments.CountAsync(e => e.Grade != null && e.Grade.LetterGrade != null && e.Grade.LetterGrade != "F" && e.Grade.LetterGrade != "f", cancellationToken).ConfigureAwait(false);

        var passRate = gradedCount > 0 ? Math.Round((decimal)passingCount / gradedCount * 100m, 2) : 0m;
        var dropoutRate = totalEnrollments > 0 ? Math.Round((decimal)droppedEnrollments / totalEnrollments * 100m, 2) : 0m;

        var graduatingStudents = await context.Enrollments
            .Where(e => e.Status == EnrollmentStatus.Completed)
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var performance = await GetCourseOfferingPerformanceAsync(cancellationToken).ConfigureAwait(false);

        return new AcademicDashboardSnapshot(
            totalEnrollments,
            activeEnrollments,
            droppedEnrollments,
            completedEnrollments,
            newEnrollmentsThisSession,
            gradeDistribution,
            passRate,
            dropoutRate,
            graduatingStudents,
            performance);
    }

    public async Task<AcademicTeachingAggregateSnapshot> GetTeachingAggregatesAsync(CancellationToken cancellationToken = default)
    {
        var teachingLoad = await context.CourseOfferings
            .Where(o => o.InstructorFacultyMemberId != null)
            .GroupBy(o => o.InstructorFacultyMemberId!.Value)
            .Select(g => new { FacultyMemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.FacultyMemberId, g => g.Count, cancellationToken).ConfigureAwait(false);

        var attendanceByStatus = await context.AttendanceSessions
            .SelectMany(s => s.Records)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var totalAttendance = attendanceByStatus.Sum(a => a.Count);
        var presentOrLate = attendanceByStatus.Where(a => a.Status is AttendanceStatus.Present or AttendanceStatus.Late).Sum(a => a.Count);
        var attendanceRate = totalAttendance > 0 ? Math.Round((decimal)presentOrLate / totalAttendance * 100m, 2) : 0m;

        var gradeDistribution = await GetGradeDistributionAsync(cancellationToken).ConfigureAwait(false);

        var totalEnrollments = await context.Enrollments.CountAsync(cancellationToken).ConfigureAwait(false);
        var completedEnrollments = await context.Enrollments.CountAsync(e => e.Status == EnrollmentStatus.Completed, cancellationToken).ConfigureAwait(false);
        var courseCompletionRate = totalEnrollments > 0 ? Math.Round((decimal)completedEnrollments / totalEnrollments * 100m, 2) : 0m;

        return new AcademicTeachingAggregateSnapshot(teachingLoad, attendanceRate, gradeDistribution, courseCompletionRate);
    }

    private async Task<Dictionary<string, int>> GetGradeDistributionAsync(CancellationToken cancellationToken) =>
        await context.Enrollments
            .Where(e => e.Grade != null && e.Grade.LetterGrade != null)
            .GroupBy(e => e.Grade!.LetterGrade!)
            .Select(g => new { Letter = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Letter, g => g.Count, cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlyList<CourseOfferingPerformance>> GetCourseOfferingPerformanceAsync(CancellationToken cancellationToken)
    {
        var offerings = await context.CourseOfferings
            .Select(o => new { OfferingId = o.Id.Value, o.CourseId, o.DepartmentId })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var enrollmentAggregates = await context.Enrollments
            .GroupBy(e => e.CourseOfferingId)
            .Select(g => new
            {
                CourseOfferingId = g.Key,
                EnrolledCount = g.Count(),
                GradedCount = g.Count(e => e.Grade != null && e.Grade.LetterGrade != null),
                PassingCount = g.Count(e => e.Grade != null && e.Grade.LetterGrade != null && e.Grade.LetterGrade != "F" && e.Grade.LetterGrade != "f"),
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var aggByOffering = enrollmentAggregates.ToDictionary(a => a.CourseOfferingId);

        return offerings.Select(o =>
        {
            var agg = aggByOffering.GetValueOrDefault(o.OfferingId);
            var gradedCount = agg?.GradedCount ?? 0;
            var passRate = gradedCount > 0 ? Math.Round((decimal)(agg?.PassingCount ?? 0) / gradedCount * 100m, 2) : 0m;
            return new CourseOfferingPerformance(o.OfferingId, o.CourseId, o.DepartmentId, agg?.EnrolledCount ?? 0, gradedCount, passRate);
        }).ToList();
    }
}
