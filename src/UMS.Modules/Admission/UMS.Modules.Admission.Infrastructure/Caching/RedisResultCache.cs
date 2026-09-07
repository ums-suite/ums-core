using StackExchange.Redis;
using UMS.Modules.Admission.Application.Abstractions;

namespace UMS.Modules.Admission.Infrastructure.Caching;

/// <summary>
/// ADR-0007's write-through cache. Shares the platform's one <see cref="IConnectionMultiplexer"/> -
/// never a second multiplexer - exactly like Identity's own <c>RedisAuthzCache</c>/Organization's
/// own <c>RedisOrganizationTreeCache</c>.
///
/// <para>
/// <b>Atomicity mechanism - a documented simplification of edge-cases.md's literal description.</b>
/// design-decisions.md's own "Cache-Regeneration Concurrency for Result Corrections" decision
/// describes a shadow-key-then-atomic-rename two-phase write. This implementation instead writes
/// all three related keys for one applicant inside a SINGLE Lua script (<see cref="WriteScript"/>) -
/// Redis executes a Lua script atomically, single-threaded, so a reader can only ever observe the
/// fully-old or fully-new set of three keys, never a torn mix, which is the exact reader-visible
/// guarantee the shadow-key/rename approach exists to provide. This is a real simplification (one
/// fewer moving part, no separate rename step) that delivers the identical externally-observable
/// property, not a weakened one.
/// </para>
/// </summary>
internal sealed class RedisResultCache(IConnectionMultiplexer redis) : IResultCache
{
    private const string WriteScript = @"
for i = 1, #KEYS do
    redis.call('SET', KEYS[i], ARGV[1])
end
return 1";

    public async Task<string?> GetByApplicationNumberAsync(string applicationNumber, CancellationToken cancellationToken = default)
    {
        var value = await redis.GetDatabase().StringGetAsync(ApplicationNumberKey(applicationNumber)).ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<string?> GetByStudentIdAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        var value = await redis.GetDatabase().StringGetAsync(StudentIdKey(studentId)).ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task<string?> GetByExamRollNumberAsync(Guid examId, string rollNumber, CancellationToken cancellationToken = default)
    {
        var value = await redis.GetDatabase().StringGetAsync(ExamRollNumberKey(examId, rollNumber)).ConfigureAwait(false);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task WriteResultAsync(ResultCacheEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var keys = new List<RedisKey> { ApplicationNumberKey(entry.ApplicationNumber) };
        if (entry.StudentId is { } studentId)
        {
            keys.Add(StudentIdKey(studentId));
        }

        if (entry.RollNumber is { } rollNumber)
        {
            keys.Add(ExamRollNumberKey(entry.ExamId, rollNumber));
        }

        await redis.GetDatabase().ScriptEvaluateAsync(WriteScript, keys.ToArray(), [entry.ResultJson]).ConfigureAwait(false);
    }

    public async Task<IAsyncDisposable?> TryAcquireRegenerationLockAsync(Guid campaignId, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = redis.GetDatabase();
        var lockKey = $"admission:result-regeneration-lock:{campaignId:N}";
        var token = Guid.NewGuid().ToString("N");
        var acquired = await db.LockTakeAsync(lockKey, token, ttl).ConfigureAwait(false);
        return acquired ? new RedisLockHandle(db, lockKey, token) : null;
    }

    private static string ApplicationNumberKey(string applicationNumber) => $"result:{applicationNumber}";

    private static string StudentIdKey(Guid studentId) => $"result:student:{studentId:N}";

    private static string ExamRollNumberKey(Guid examId, string rollNumber) => $"result:exam:{examId:N}:{rollNumber}";

    private sealed class RedisLockHandle(IDatabase database, string lockKey, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await database.LockReleaseAsync(lockKey, token).ConfigureAwait(false);
    }
}
