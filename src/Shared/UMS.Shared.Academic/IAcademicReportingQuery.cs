namespace UMS.Shared.Academic;

/// <summary>
/// RPT-3/RPT-4/RPT-7: Reporting's own scheduled <c>MetricRefreshJob</c>s call this - never a raw
/// SQL join into Academic's schema (reporting requirement-spec.md §4's foundational invariant).
/// Living in <c>UMS.Shared.Academic</c>, not <c>UMS.Modules.Academic.*</c>, lets Reporting call it
/// without a forbidden dependency on Academic's Domain/Application/Infrastructure internals
/// (module-boundaries.md, ADR-0002), mirroring <see cref="ICourseOfferingLookup"/>'s exact pattern.
/// Academic's own Infrastructure layer registers the one real implementation, computing every
/// aggregate figure itself (JOIN/GROUP BY happens inside Academic's own schema) and returning only
/// already-aggregated numbers - Reporting never receives a raw entity row.
///
/// <para>
/// <b>Read-consistency simplification (documented, not silent):</b> reporting design-decisions.md's
/// "Snapshot-Timestamp Pinning" decision would ideally have every call here accept an <c>asOf</c>
/// parameter. This first pass adopts edge-cases.md's own sanctioned coarser fallback instead - no
/// <c>asOf</c> parameter at all, a plain "current state" read - since Academic's storage has no
/// read-replica/WAL-position point-in-time capability. The calling <c>MetricRefreshJob</c> captures
/// its own wall-clock <c>data_as_of</c> immediately before this call; snapshot skew across a single
/// job's several source-module calls is bounded by that job's own total run duration. Genuine
/// multi-call transactional pinning is a documented gap for a future revision.
/// </para>
///
/// <para>
/// Also serves reporting requirement-spec.md's Faculty dashboard (RPT-7): teaching-load,
/// attendance, grade-distribution, and course-completion figures are actually Academic-owned data
/// (Grade/Attendance/Enrollment/CourseOffering), resolved via the Faculty↔CourseOffering
/// <c>InstructorFacultyMemberId</c> linkage Academic itself already owns (see
/// <c>CourseOffering</c>'s own remarks) - the Faculty dashboard is NOT self-contained inside
/// Faculty's own schema, so its teaching-side figures are served by THIS contract, not
/// <see cref="Faculty.IFacultyReportingQuery"/>.
/// </para>
/// </summary>
public interface IAcademicReportingQuery
{
    /// <summary>RPT-4: enrollment counts, GPA/grade distribution, pass/dropout rate, new enrollments this session, graduating students, course/department performance.</summary>
    public Task<AcademicDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>RPT-7: the Academic-owned half of the Faculty dashboard - teaching load, attendance, grade distribution, and course completion, resolved via each CourseOffering's own <c>InstructorFacultyMemberId</c>.</summary>
    public Task<AcademicTeachingAggregateSnapshot> GetTeachingAggregatesAsync(CancellationToken cancellationToken = default);
}

/// <summary>RPT-4's Academic dashboard read model. <see cref="GraduatingStudents"/> approximates "graduating students" as the distinct count of Students holding a <c>Completed</c> Enrollment - Academic's own schema has no separate graduation-conferral record, a documented first-pass simplification.</summary>
public sealed record AcademicDashboardSnapshot(
    int TotalEnrollments,
    int ActiveEnrollments,
    int DroppedEnrollments,
    int CompletedEnrollments,
    int NewEnrollmentsThisSession,
    IReadOnlyDictionary<string, int> GradeDistribution,
    decimal PassRate,
    decimal DropoutRate,
    int GraduatingStudents,
    IReadOnlyList<CourseOfferingPerformance> CourseOfferingPerformance);

/// <summary>One CourseOffering's own performance figures - the department/course-performance breakdown RPT-4 names.</summary>
public sealed record CourseOfferingPerformance(Guid CourseOfferingId, Guid CourseId, Guid DepartmentId, int EnrolledCount, int GradedCount, decimal PassRate);

/// <summary>RPT-7's Academic-owned half of the Faculty dashboard.</summary>
public sealed record AcademicTeachingAggregateSnapshot(
    IReadOnlyDictionary<Guid, int> TeachingLoadByFacultyMember,
    decimal OverallAttendanceRate,
    IReadOnlyDictionary<string, int> GradeDistribution,
    decimal CourseCompletionRate);
