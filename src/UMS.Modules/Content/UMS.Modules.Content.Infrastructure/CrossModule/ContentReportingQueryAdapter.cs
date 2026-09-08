using Microsoft.EntityFrameworkCore;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Infrastructure.Persistence;
using UMS.Shared.Content;

namespace UMS.Modules.Content.Infrastructure.CrossModule;

/// <summary>
/// Flow #26: the one real implementation of <see cref="IContentReportingQuery"/> - mirrors
/// <c>UMS.Modules.Faculty.Infrastructure.CrossModule.FacultyReportingQueryAdapter</c>'s exact
/// pattern. See the shared interface's own remarks for why this is a deliberate scope extension
/// (a seventh, Content-owned admin dashboard) rather than an item named in Reporting's original
/// six-dashboard list.
/// </summary>
internal sealed class ContentReportingQueryAdapter(ContentDbContext context) : IContentReportingQuery
{
    public async Task<ContentDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // design-decisions.md's Snapshot-Timestamp Pinning: captured once for this run's own
        // upcoming-Event check, mirroring Event.IsUpcoming(now) exactly rather than a second,
        // independently-drifting "now".
        var now = DateTimeOffset.UtcNow;

        var publishedNoticeCount = await context.Notices
            .CountAsync(n => n.Status == SchedulableStatus.Published, cancellationToken).ConfigureAwait(false);

        // requirement-spec.md §2.1/§4: Published already IS the active window - the scheduled job,
        // never a live ad hoc publish_at/expire_at recheck, is what keeps that status current.
        var activeBannerCount = await context.Banners
            .CountAsync(b => b.Status == SchedulableStatus.Published, cancellationToken).ConfigureAwait(false);

        var upcomingEventCount = await context.Events
            .CountAsync(e => e.EndAt >= now, cancellationToken).ConfigureAwait(false);

        var publishedDownloadResourceCount = await context.DownloadResources
            .CountAsync(d => d.Status == SchedulableStatus.Published, cancellationToken).ConfigureAwait(false);

        return new ContentDashboardSnapshot(publishedNoticeCount, activeBannerCount, upcomingEventCount, publishedDownloadResourceCount);
    }
}
