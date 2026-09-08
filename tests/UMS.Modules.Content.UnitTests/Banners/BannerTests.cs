using UMS.Modules.Content.Domain.Banners;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;

namespace UMS.Modules.Content.UnitTests.Banners;

/// <summary>CNT-8/CNT-9: requirement-spec.md §2.3 - the identical Draft->Scheduled->Published->Archived state machine as Notice, no bilingual gate (Banner is not a localized entity).</summary>
public sealed class BannerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private static Banner Draft() => Banner.Create("Headline", "https://cdn/banner.png", null, 1, Actor, Now).Value;

    [Fact]
    public void Full_lifecycle_Draft_Scheduled_Published_Archived_succeeds_in_order()
    {
        var banner = Draft();
        banner.UpdateSchedule(Now.AddDays(1), null, Now);

        Assert.True(banner.Schedule(Now).IsSuccess);
        Assert.True(banner.Publish(Now).IsSuccess);
        Assert.Contains(banner.DomainEvents, e => e is BannerPublished);
        Assert.True(banner.Archive(Now).IsSuccess);
        Assert.Equal(SchedulableStatus.Archived, banner.Status);
    }

    [Fact]
    public void Schedule_without_publish_at_is_rejected()
    {
        var banner = Draft();

        var result = banner.Schedule(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("banner.publish_at_required", result.Error!.Code);
    }

    [Fact]
    public void UpdateDetails_after_Archived_is_rejected()
    {
        var banner = Draft();
        banner.UpdateSchedule(Now.AddDays(1), null, Now);
        banner.Schedule(Now);
        banner.Publish(Now);
        banner.Archive(Now);

        var result = banner.UpdateDetails("New", "https://cdn/new.png", null, 2, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("banner.archived", result.Error!.Code);
    }

    [Fact]
    public void UpdateSchedule_rejects_expireAt_not_strictly_after_publishAt()
    {
        var banner = Draft();

        var result = banner.UpdateSchedule(Now, Now, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("banner.invalid_window", result.Error!.Code);
    }
}
