using UMS.Modules.Reporting.Application.Abstractions;

namespace UMS.Modules.Reporting.UnitTests.Fakes;

/// <summary>An in-memory stand-in for the Redis lease - held/released state only, no TTL expiry simulation (that belongs to the real-Redis integration suite).</summary>
public sealed class FakeMetricRefreshLease : IMetricRefreshLease
{
    private readonly HashSet<string> _held = [];

    public int AcquireAttempts { get; private set; }

    public string? LastRequestedKey { get; private set; }

    public TimeSpan? LastRequestedTtl { get; private set; }

    public Task<IAsyncDisposable?> TryAcquireAsync(string metricKey, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        AcquireAttempts++;
        LastRequestedKey = metricKey;
        LastRequestedTtl = ttl;

        if (!_held.Add(metricKey))
        {
            return Task.FromResult<IAsyncDisposable?>(null);
        }

        return Task.FromResult<IAsyncDisposable?>(new Handle(this, metricKey));
    }

    /// <summary>Simulates "another job's run is still in flight" - held until the returned handle is disposed.</summary>
    public IAsyncDisposable HoldExternally(string metricKey)
    {
        _held.Add(metricKey);
        return new Handle(this, metricKey);
    }

    private sealed class Handle(FakeMetricRefreshLease owner, string metricKey) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            owner._held.Remove(metricKey);
            return ValueTask.CompletedTask;
        }
    }
}
