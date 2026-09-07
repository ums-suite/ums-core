using UMS.Modules.Admission.Domain.Publishing;

namespace UMS.Modules.Admission.UnitTests.Results;

/// <summary>design-decisions.md "Write-Through Cache Publish Atomicity": the resumable, checkpointed batch progress mechanism.</summary>
public sealed class PublishJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Advancing_to_FanningOut_before_the_cache_is_fully_warmed_is_rejected()
    {
        var job = PublishJob.Create(Guid.NewGuid(), Guid.NewGuid(), totalCount: 10, Now).Value;
        job.RecordCacheProgress(5, Guid.NewGuid());

        var result = job.CompleteCacheWarm();

        Assert.True(result.IsFailure);
        Assert.Equal("publish_job.cache_warm_incomplete", result.Error!.Code);
        Assert.Equal(PublishJobStage.WarmingCache, job.Stage);
    }

    [Fact]
    public void The_job_advances_through_every_stage_once_each_batch_completes()
    {
        var job = PublishJob.Create(Guid.NewGuid(), Guid.NewGuid(), totalCount: 3, Now).Value;

        job.RecordCacheProgress(3, Guid.NewGuid());
        Assert.True(job.CompleteCacheWarm().IsSuccess);
        Assert.Equal(PublishJobStage.FanningOut, job.Stage);

        job.RecordFanOutProgress(3, Guid.NewGuid());
        Assert.True(job.Complete(Now).IsSuccess);
        Assert.Equal(PublishJobStage.Completed, job.Stage);
    }

    [Fact]
    public void A_zero_applicant_campaign_is_immediately_cache_warm_complete()
    {
        var job = PublishJob.Create(Guid.NewGuid(), Guid.NewGuid(), totalCount: 0, Now).Value;

        Assert.True(job.IsCacheWarmComplete);
    }

    [Fact]
    public void Recording_cache_progress_while_FanningOut_is_rejected()
    {
        var job = PublishJob.Create(Guid.NewGuid(), Guid.NewGuid(), totalCount: 1, Now).Value;
        job.RecordCacheProgress(1, Guid.NewGuid());
        job.CompleteCacheWarm();

        var result = job.RecordCacheProgress(1, Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("publish_job.not_warming_cache", result.Error!.Code);
    }

    [Fact]
    public void A_failed_job_records_the_reason_and_a_completion_timestamp()
    {
        var job = PublishJob.Create(Guid.NewGuid(), Guid.NewGuid(), totalCount: 1, Now).Value;

        job.Fail("Redis unreachable", Now);

        Assert.Equal(PublishJobStage.Failed, job.Stage);
        Assert.Equal("Redis unreachable", job.FailureReason);
        Assert.NotNull(job.CompletedAt);
    }
}
