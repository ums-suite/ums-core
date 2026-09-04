using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Domain.Preferences;

/// <summary>
/// §8 edge case: "Hard email bounce -&gt; the recipient's email is marked suppressed for future
/// automated sends until updated via Identity's own profile flow, rather than retried against a
/// permanently bad address." One row per (<see cref="RecipientId"/>, <see cref="Channel"/>);
/// checked by the dispatch pipeline alongside contact info/opt-out, immediately before every send
/// (NTF-15's webhook handler is what creates these rows).
/// </summary>
public sealed class ChannelSuppression : AggregateRoot<ChannelSuppressionId>
{
    private ChannelSuppression()
    {
    }

    public Guid RecipientId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset SuppressedAt { get; private set; }

    public static ChannelSuppression Create(Guid recipientId, NotificationChannel channel, string reason, DateTimeOffset now) => new()
    {
        Id = ChannelSuppressionId.New(),
        RecipientId = recipientId,
        Channel = channel,
        Reason = reason,
        SuppressedAt = now,
    };
}
