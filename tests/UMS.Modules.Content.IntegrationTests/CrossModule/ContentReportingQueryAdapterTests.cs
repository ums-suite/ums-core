using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Content.Application.Banners;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Application.Downloads;
using UMS.Modules.Content.Application.Events;
using UMS.Modules.Content.Application.Notices;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.IntegrationTests.Infrastructure;
using UMS.Shared.Content;

namespace UMS.Modules.Content.IntegrationTests.CrossModule;

/// <summary>
/// Flow #26: exercises <c>ContentReportingQueryAdapter</c> - the one real implementation of
/// <see cref="IContentReportingQuery"/> - against a real Postgres row set, mirroring this fixture's
/// own Application-service-level posture exactly.
/// </summary>
[Collection(ContentApiTestCollectionDefinition.Name)]
public sealed class ContentReportingQueryAdapterTests(ContentServiceFixture fixture)
{
    private static AuditContext NewAudit() => new(Guid.NewGuid(), ActorIpAddress: null, Guid.NewGuid().ToString());

    [Fact]
    public async Task Snapshot_counts_only_Published_and_upcoming_rows()
    {
        using var scope = fixture.Services.CreateScope();
        var notices = scope.ServiceProvider.GetRequiredService<NoticeService>();
        var banners = scope.ServiceProvider.GetRequiredService<BannerService>();
        var events = scope.ServiceProvider.GetRequiredService<EventService>();
        var downloads = scope.ServiceProvider.GetRequiredService<DownloadResourceService>();
        var query = scope.ServiceProvider.GetRequiredService<IContentReportingQuery>();

        // Deltas, not absolute totals - this fixture's Postgres instance is shared across every test
        // in this collection (ContentApiTestCollectionDefinition).
        var before = await query.GetDashboardSnapshotAsync();

        var actorUserId = Guid.NewGuid();

        // Admin audience (never Public) deliberately avoids the bilingual-completeness gate, which
        // this test has no need to exercise.
        var publishedNotice = await notices.CreateAsync("A Notice", "Body", ContentAudience.Admin, null, isUrgent: false, actorUserId);
        Assert.True(publishedNotice.IsSuccess);
        var publishedNoticeResult = await notices.PublishAsync(publishedNotice.Value.Id, NewAudit(), publishedNotice.Value.Version);
        Assert.True(publishedNoticeResult.IsSuccess);

        // A Draft Notice must NOT be counted.
        var draftNotice = await notices.CreateAsync("Draft Notice", "Body", ContentAudience.Admin, null, isUrgent: false, actorUserId);
        Assert.True(draftNotice.IsSuccess);

        var publishedBanner = await banners.CreateAsync("Headline", "https://example.test/img.png", null, 0, actorUserId);
        Assert.True(publishedBanner.IsSuccess);
        var publishedBannerResult = await banners.PublishAsync(publishedBanner.Value.Id, NewAudit(), publishedBanner.Value.Version);
        Assert.True(publishedBannerResult.IsSuccess);

        var draftBanner = await banners.CreateAsync("Draft Headline", "https://example.test/img2.png", null, 1, actorUserId);
        Assert.True(draftBanner.IsSuccess);

        var now = DateTimeOffset.UtcNow;
        var upcomingEvent = await events.CreateAsync("An Event", "Body", null, ContentAudience.Admin, null, now.AddDays(1), now.AddDays(2), actorUserId);
        Assert.True(upcomingEvent.IsSuccess);

        // Already ended - must NOT be counted as upcoming.
        var pastEvent = await events.CreateAsync("A Past Event", "Body", null, ContentAudience.Admin, null, now.AddDays(-3), now.AddDays(-2), actorUserId);
        Assert.True(pastEvent.IsSuccess);

        var publishedDownload = await downloads.CreateAsync("A Form", "Forms", Guid.NewGuid(), actorUserId);
        Assert.True(publishedDownload.IsSuccess);
        var publishedDownloadResult = await downloads.PublishAsync(publishedDownload.Value.Id, publishedDownload.Value.Version);
        Assert.True(publishedDownloadResult.IsSuccess);

        var draftDownload = await downloads.CreateAsync("A Draft Form", "Forms", Guid.NewGuid(), actorUserId);
        Assert.True(draftDownload.IsSuccess);

        var after = await query.GetDashboardSnapshotAsync();

        Assert.Equal(1, after.PublishedNoticeCount - before.PublishedNoticeCount);
        Assert.Equal(1, after.ActiveBannerCount - before.ActiveBannerCount);
        Assert.Equal(1, after.UpcomingEventCount - before.UpcomingEventCount);
        Assert.Equal(1, after.PublishedDownloadResourceCount - before.PublishedDownloadResourceCount);
    }
}
