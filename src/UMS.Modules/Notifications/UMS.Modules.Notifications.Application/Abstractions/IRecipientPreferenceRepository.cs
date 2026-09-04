using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.Application.Abstractions;

public interface IRecipientPreferenceRepository
{
    public void Add(RecipientNotificationPreference preference);

    public Task<RecipientNotificationPreference?> GetAsync(Guid recipientId, NotificationCategory category, CancellationToken cancellationToken = default);

    /// <summary>Send-time authoritative opt-out check (design-decisions.md "Opt-Out-Check Timing") - a category with no stored preference row has never been opted out of.</summary>
    public Task<bool> IsOptedOutAsync(Guid recipientId, NotificationCategory category, CancellationToken cancellationToken = default);
}
