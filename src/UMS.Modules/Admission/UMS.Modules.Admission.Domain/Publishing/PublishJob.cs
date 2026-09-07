using UMS.Modules.Admission.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Admission.Domain.Publishing;

/// <summary>
/// design-decisions.md "Write-Through Cache Publish Atomicity": "A durable PublishJob aggregate
/// tracks per-applicant generation progress for a campaign's bulk publish ... idempotent and
/// resumable from the last checkpointed applicant on crash/restart, never restarting the whole
/// batch from zero." Also the job design-decisions.md's "Background Job Design" decision reuses
/// for the SAME batch's document/notification fan-out, one stage after the cache is fully warmed.
///
/// <para>
/// <see cref="CacheCursor"/>/<see cref="FanOutCursor"/> are the last processed
/// <c>AdmissionResultEntry.ApplicantId</c> in a stable ordering (ascending by id) - a crash/restart
/// resumes a batch worker's own next poll from strictly-after that cursor, never restarting the
/// whole campaign's batch (design-decisions.md's own stated efficiency requirement).
/// </para>
/// </summary>
public sealed class PublishJob : AggregateRoot<PublishJobId>
{
    private PublishJob()
    {
    }

    private PublishJob(PublishJobId id, Guid admissionResultId, Guid campaignId, int totalCount, DateTimeOffset now)
    {
        Id = id;
        AdmissionResultId = admissionResultId;
        CampaignId = campaignId;
        TotalCount = totalCount;
        Stage = PublishJobStage.WarmingCache;
        StartedAt = now;
    }

    public Guid AdmissionResultId { get; private set; }

    public Guid CampaignId { get; private set; }

    public PublishJobStage Stage { get; private set; }

    public int TotalCount { get; private set; }

    public int CacheProcessedCount { get; private set; }

    public Guid? CacheCursor { get; private set; }

    public int FanOutProcessedCount { get; private set; }

    public Guid? FanOutCursor { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? FailureReason { get; private set; }

    public bool IsCacheWarmComplete => CacheProcessedCount >= TotalCount;

    public bool IsFanOutComplete => FanOutProcessedCount >= TotalCount;

    public static Result<PublishJob> Create(Guid admissionResultId, Guid campaignId, int totalCount, DateTimeOffset now)
    {
        if (totalCount < 0)
        {
            return Error.Validation("publish_job.total_count_invalid", "A PublishJob's totalCount cannot be negative.");
        }

        return new PublishJob(PublishJobId.New(), admissionResultId, campaignId, totalCount, now);
    }

    /// <summary>Called after a batch of applicants' Redis keys are durably written and verified - never before (design-decisions.md's all-or-nothing external guarantee).</summary>
    public Result RecordCacheProgress(int processedInBatch, Guid lastCursor)
    {
        if (Stage != PublishJobStage.WarmingCache)
        {
            return Result.Failure(Error.Conflict("publish_job.not_warming_cache", $"PublishJob '{Id}' is not WarmingCache (currently '{Stage}')."));
        }

        CacheProcessedCount += processedInBatch;
        CacheCursor = lastCursor;
        return Result.Success();
    }

    public Result CompleteCacheWarm()
    {
        if (Stage != PublishJobStage.WarmingCache || !IsCacheWarmComplete)
        {
            return Result.Failure(Error.Conflict("publish_job.cache_warm_incomplete", $"PublishJob '{Id}' cannot advance to FanningOut - {CacheProcessedCount}/{TotalCount} applicants cache-verified."));
        }

        Stage = PublishJobStage.FanningOut;
        return Result.Success();
    }

    public Result RecordFanOutProgress(int processedInBatch, Guid lastCursor)
    {
        if (Stage != PublishJobStage.FanningOut)
        {
            return Result.Failure(Error.Conflict("publish_job.not_fanning_out", $"PublishJob '{Id}' is not FanningOut (currently '{Stage}')."));
        }

        FanOutProcessedCount += processedInBatch;
        FanOutCursor = lastCursor;
        return Result.Success();
    }

    public Result Complete(DateTimeOffset now)
    {
        if (Stage != PublishJobStage.FanningOut || !IsFanOutComplete)
        {
            return Result.Failure(Error.Conflict("publish_job.fan_out_incomplete", $"PublishJob '{Id}' cannot complete - {FanOutProcessedCount}/{TotalCount} applicants fanned out."));
        }

        Stage = PublishJobStage.Completed;
        CompletedAt = now;
        return Result.Success();
    }

    /// <summary>design-decisions.md's elevated-tier decision: a stalled/failed PublishJob pages on-call directly - this is the state a monitor watches for.</summary>
    public void Fail(string reason, DateTimeOffset now)
    {
        Stage = PublishJobStage.Failed;
        FailureReason = reason;
        CompletedAt = now;
    }
}
