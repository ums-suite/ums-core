namespace UMS.Shared.Career;

/// <summary>
/// release/DEVELOPMENT_PLAN.md Flow #31 ("Reporting - Alumni &amp; Career top-up"): mirrors
/// <c>UMS.Shared.Content.IContentReportingQuery</c>'s exact pattern (itself Flow #26's own precedent) -
/// lives here (not <c>UMS.Modules.Career.*</c>) so Reporting can call it without a forbidden
/// dependency on Career's internals (module-boundaries.md, ADR-0002). Career's own Infrastructure
/// layer registers the one real implementation.
///
/// <para>
/// <b>A documented, deliberate scope extension, not an oversight - the same judgment call Flow #26
/// already made for Content, applied identically here:</b> Career is named as a Reporting dependency
/// in Reporting's own requirement-spec.md §7, but Reporting's own §2.2 dashboard table names only six
/// dashboards (Academic/Admission/Financial/Faculty/Hostel/Library) - Career has no explicitly-named
/// dashboard or regulatory category anywhere in that spec. Unlike Flow #26's Research half, there is
/// no existing regulatory-report seeder proxy referencing Career data to fix here either
/// (<c>RegulatoryReportDefinitionCatalogSeeder</c> names zero Career fields) - this is purely additive,
/// a ninth admin dashboard, closing the same shape of dependency gap Flow #26 closed for Content.
/// </para>
///
/// <para>
/// Deliberately scoped to plain aggregate counts/sums Career's own Infrastructure can cheaply compute,
/// never a raw entity row (reporting requirement-spec.md §4's "never a live cross-schema join"
/// invariant, shared by every reporting-query contract in this codebase). No <c>asOf</c> parameter -
/// see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks for the documented
/// read-consistency simplification every one of them shares.
/// </para>
/// </summary>
public interface ICareerReportingQuery
{
    /// <summary>Internship/CampusRecruitmentDrive/CareerApplication counts plus total InterviewSlot bookings - the whole career-dashboard payload.</summary>
    public Task<CareerDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="PublishedInternshipCount"/> mirrors <c>Internship.IsBrowsable()</c> exactly - a
/// <c>Published</c> OR <c>ApplicationsOpen</c> posting is still publicly visible, never re-derived
/// from <c>ApplicationDeadline</c> directly (CAR-3's scheduled sweep, not a GET, is what keeps that
/// status current). <see cref="TotalInterviewSlotBookings"/> sums each <c>InterviewSlot</c>'s own
/// already-computed <c>BookedCount</c> (design-decisions.md "Interview-Slot Booking Concurrency
/// Control" - mutated ONLY by the atomic conditional-write repository method, never recomputed here
/// from a live join against <c>CareerApplication.InterviewSlotId</c>).
/// </summary>
public sealed record CareerDashboardSnapshot(
    int TotalInternshipCount,
    int PublishedInternshipCount,
    int TotalCampusRecruitmentDriveCount,
    int TotalCareerApplicationCount,
    int TotalInterviewSlotBookings);
