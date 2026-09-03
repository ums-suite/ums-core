using System.Threading.RateLimiting;

namespace UMS.Shared.Resilience.RateLimiting;

internal sealed class RedisRateLimitLease(bool isAcquired, TimeSpan? retryAfter) : RateLimitLease
{
    public override bool IsAcquired { get; } = isAcquired;

    public override IEnumerable<string> MetadataNames =>
        retryAfter.HasValue ? [MetadataName.RetryAfter.Name] : [];

    public override bool TryGetMetadata(string metadataName, out object? metadata)
    {
        if (metadataName == MetadataName.RetryAfter.Name && retryAfter.HasValue)
        {
            metadata = retryAfter.Value;
            return true;
        }

        metadata = null;
        return false;
    }
}
