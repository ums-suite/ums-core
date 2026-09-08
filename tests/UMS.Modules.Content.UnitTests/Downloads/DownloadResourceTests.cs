using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Downloads;

namespace UMS.Modules.Content.UnitTests.Downloads;

/// <summary>CNT-11: requirement-spec.md §2.6 - the identical scheduling state machine as Notice/Banner; metadata + a Documents artifact reference only.</summary>
public sealed class DownloadResourceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Actor = Guid.NewGuid();

    private static DownloadResource Draft() => DownloadResource.Create("Admission Form", "Forms", Guid.NewGuid(), Actor, Now).Value;

    [Fact]
    public void Create_requires_a_non_empty_artifact_id()
    {
        var result = DownloadResource.Create("Title", "Forms", Guid.Empty, Actor, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("download.artifact_required", result.Error!.Code);
    }

    [Fact]
    public void Full_lifecycle_Draft_Scheduled_Published_Archived_succeeds_in_order()
    {
        var resource = Draft();
        resource.UpdateSchedule(Now.AddDays(1), null, Now);

        Assert.True(resource.Schedule(Now).IsSuccess);
        Assert.True(resource.Publish(Now).IsSuccess);
        Assert.True(resource.Archive(Now).IsSuccess);
        Assert.Equal(SchedulableStatus.Archived, resource.Status);
    }

    [Fact]
    public void Publish_from_Archived_is_rejected()
    {
        var resource = Draft();
        resource.Publish(Now);
        resource.Archive(Now);

        var result = resource.Publish(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("download.not_publishable", result.Error!.Code);
    }
}
