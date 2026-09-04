using System.Threading.RateLimiting;
using StackExchange.Redis;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Shared.Resilience.RateLimiting;

namespace UMS.Modules.Notifications.Infrastructure.RateLimiting;

/// <summary>
/// The one real implementation of <see cref="IOtpRateLimiter"/> - reuses
/// <see cref="UmsRedisRateLimiterFactory"/>, the exact same Redis-backed fixed-window limiter
/// ums-conventions.md's Resilience section describes, partitioned per recipient.
/// </summary>
internal sealed class OtpRateLimiter : IOtpRateLimiter
{
    /// <summary>BRD §11's own number is not specified further than "prevent abuse" - 3 OTPs per 10 minutes per recipient is this build's concrete, documented default.</summary>
    private const int PermitLimit = 3;

    private static readonly TimeSpan _window = TimeSpan.FromMinutes(10);

    private readonly PartitionedRateLimiter<Guid> _limiter;

    public OtpRateLimiter(IConnectionMultiplexer redis)
    {
        _limiter = UmsRedisRateLimiterFactory.Create<Guid>(redis, "notifications-otp", recipientId => recipientId.ToString(), PermitLimit, _window);
    }

    public async Task<bool> TryAcquireAsync(Guid recipientId, CancellationToken cancellationToken = default)
    {
        using var lease = await _limiter.AcquireAsync(recipientId, 1, cancellationToken).ConfigureAwait(false);
        return lease.IsAcquired;
    }
}
