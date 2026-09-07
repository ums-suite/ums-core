namespace UMS.Modules.Admission.Application.Abstractions;

/// <summary>
/// ADR-0007's write-through cache - the ONLY place `AdmissionResult` becomes visible to an
/// applicant. design-decisions.md's "Write-Through Cache Publish Atomicity"/"Cache-Regeneration
/// Concurrency for Result Corrections" both name a Lua-scripted atomic multi-key write; this
/// interface's <see cref="WriteResultAsync"/> is that single indivisible write (all three keys in
/// one Redis <c>EVAL</c>, which Redis itself executes atomically single-threaded) - a reader can
/// only ever observe the fully-old or fully-new record for one applicant, never a torn mix, without
/// this build needing a separate shadow-key/rename phase to get that same guarantee (a documented
/// simplification over the edge-cases.md's literal two-phase description - see the Infrastructure
/// implementation's own remarks).
/// </summary>
public interface IResultCache
{
    /// <summary>requirement-spec.md §5: served exclusively from Redis post-publish - a <c>null</c> return while the campaign's AdmissionResult is Published is the "genuine cache miss" edge case, an operational bug per ADR-0007, never silently served from Postgres.</summary>
    public Task<string?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken cancellationToken = default);

    public Task<string?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default);

    public Task<string?> GetByExamRollNumberAsync(Guid examId, string rollNumber, CancellationToken cancellationToken = default);

    /// <summary>Atomically writes all three lookup keys for one applicant's result - used identically for a fresh publish and for a correction's regeneration (design-decisions.md).</summary>
    public Task WriteResultAsync(ResultCacheEntry entry, CancellationToken cancellationToken = default);

    /// <summary>ADR-0007's cache-stampede protection: a distributed lock held around a correction's regeneration (never around the public read path - requirement-spec.md §5's rate-limiting/traffic NFRs make read-path locking a poor fit).</summary>
    public Task<IAsyncDisposable?> TryAcquireRegenerationLockAsync(Guid campaignId, TimeSpan ttl, CancellationToken cancellationToken = default);
}

public sealed record ResultCacheEntry(string ApplicationNumber, Guid? StudentId, Guid ExamId, string? RollNumber, string ResultJson);
