namespace UMS.Shared.Alumni;

/// <summary>
/// release/DEVELOPMENT_PLAN.md Flow #31 ("Reporting - Alumni &amp; Career top-up"): mirrors
/// <c>UMS.Shared.Content.IContentReportingQuery</c>'s exact pattern (itself Flow #26's own precedent) -
/// lives here (not <c>UMS.Modules.Alumni.*</c>) so Reporting can call it without a forbidden
/// dependency on Alumni's internals (module-boundaries.md, ADR-0002). Alumni's own Infrastructure
/// layer registers the one real implementation.
///
/// <para>
/// <b>A documented, deliberate scope extension, not an oversight - the same judgment call Flow #26
/// already made for Content, applied identically here:</b> Alumni is named as a Reporting dependency
/// in Reporting's own requirement-spec.md §7, but Reporting's own §2.2 dashboard table names only six
/// dashboards (Academic/Admission/Financial/Faculty/Hostel/Library) - Alumni has no explicitly-named
/// dashboard or regulatory category anywhere in that spec. Unlike Flow #26's Research half, there is
/// no existing regulatory-report seeder proxy referencing Alumni data to fix here either
/// (<c>RegulatoryReportDefinitionCatalogSeeder</c> names zero Alumni fields) - this is purely additive,
/// an eighth admin dashboard, closing the same shape of dependency gap Flow #26 closed for Content.
/// </para>
///
/// <para>
/// Deliberately scoped to plain aggregate counts/sums Alumni's own Infrastructure can cheaply compute,
/// never a raw entity row (reporting requirement-spec.md §4's "never a live cross-schema join"
/// invariant, shared by every reporting-query contract in this codebase). No <c>asOf</c> parameter -
/// see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks for the documented
/// read-consistency simplification every one of them shares.
/// </para>
/// </summary>
public interface IAlumniReportingQuery
{
    /// <summary>Total Alumnus/active JobPosting/active MentorshipMatch counts plus confirmed Donation totals per currency - the whole alumni-dashboard payload.</summary>
    public Task<AlumniDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="ActiveJobPostingCount"/> reads <c>JobPosting.Status == Published</c> directly (the
/// entity's own already-computed lifecycle state, never a live expiry recheck - ALM-6's scheduled
/// sweep, not a GET, is what keeps that status current). <see cref="ActiveMentorshipMatchCount"/>
/// mirrors <c>MentorshipMatch.Status == Active</c> exactly (both sides already accepted).
/// <see cref="ConfirmedDonationAmountByCurrency"/> sums only <c>DonationStatus.Confirmed</c> rows -
/// design-decisions.md "Donation Confirmation Consistency Model" - a Pending or Failed Donation never
/// contributes, mirroring <c>UMS.Shared.Research.IResearchReportingQuery</c>'s own
/// confirmed-funding-by-currency shape exactly.
/// </summary>
public sealed record AlumniDashboardSnapshot(
    int TotalAlumnusCount,
    IReadOnlyDictionary<string, decimal> ConfirmedDonationAmountByCurrency,
    int ActiveJobPostingCount,
    int ActiveMentorshipMatchCount);
