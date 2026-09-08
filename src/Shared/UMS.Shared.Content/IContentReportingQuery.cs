namespace UMS.Shared.Content;

/// <summary>
/// Flow #26 ("Reporting - Content &amp; Research top-up"): mirrors
/// <c>UMS.Shared.Faculty.IFacultyReportingQuery</c>'s exact pattern - lives here (not
/// <c>UMS.Modules.Content.*</c>) so Reporting can call it without a forbidden dependency on
/// Content's internals (module-boundaries.md, ADR-0002). Content's own Infrastructure layer
/// registers the one real implementation.
///
/// <para>
/// <b>A documented, deliberate scope extension, not an oversight:</b> Content is named as a
/// Reporting dependency in Reporting's own requirement-spec.md §7 ("Identity, Organization,
/// Admission, Academic, Student, Faculty, Finance, Hostel, Library, Alumni, Content, Documents"),
/// but Reporting's own §2.2 dashboard table names only six dashboards (Academic/Admission/
/// Financial/Faculty/Hostel/Library) - Content has no explicitly-named dashboard or regulatory
/// category anywhere in that spec, a genuine scoping gap the original spec never resolved. Flow #26
/// exists specifically to close dependency gaps of exactly this shape, so this contract and the
/// seventh "content" admin dashboard it feeds are a deliberate, in-scope extension - not scope
/// creep - made explicit here rather than silently assumed.
/// </para>
///
/// <para>
/// Deliberately scoped to plain aggregate counts Content's own Infrastructure can cheaply compute,
/// never a raw entity row (reporting requirement-spec.md §4's "never a live cross-schema join"
/// invariant, shared by every reporting-query contract in this codebase). No <c>asOf</c> parameter -
/// see <c>UMS.Shared.Academic.IAcademicReportingQuery</c>'s own remarks for the documented
/// read-consistency simplification every one of them shares.
/// </para>
/// </summary>
public interface IContentReportingQuery
{
    /// <summary>Published Notice/Banner/DownloadResource counts and the upcoming Event count - the whole content-dashboard payload.</summary>
    public Task<ContentDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// <see cref="ActiveBannerCount"/>/<see cref="PublishedDownloadResourceCount"/> read the entity's own
/// already-computed <c>SchedulableStatus.Published</c> value, never a live
/// <c>publishAt &lt;= now &lt;= expireAt</c> recheck - Content's own requirement-spec.md §2.1/§4 is
/// explicit that the scheduled publish/expire transition is the only thing ever allowed to flip that
/// status ("a GET ... must never itself flip a Scheduled notice to Published as a side effect of
/// being read"), so this snapshot honors that same invariant rather than quietly recomputing it.
/// <see cref="UpcomingEventCount"/> mirrors <c>Event.IsUpcoming</c> exactly (its own end-of-window
/// check, since Event has no separate publish/archive state machine).
/// </summary>
public sealed record ContentDashboardSnapshot(
    int PublishedNoticeCount,
    int ActiveBannerCount,
    int UpcomingEventCount,
    int PublishedDownloadResourceCount);
