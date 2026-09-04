using Microsoft.EntityFrameworkCore;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Repositories;

internal sealed class RecipientPreferenceRepository(NotificationsDbContext context) : IRecipientPreferenceRepository
{
    public void Add(RecipientNotificationPreference preference) => context.RecipientPreferences.Add(preference);

    public Task<RecipientNotificationPreference?> GetAsync(Guid recipientId, NotificationCategory category, CancellationToken cancellationToken = default) =>
        context.RecipientPreferences.FirstOrDefaultAsync(p => p.RecipientId == recipientId && p.Category == category, cancellationToken);

    public Task<bool> IsOptedOutAsync(Guid recipientId, NotificationCategory category, CancellationToken cancellationToken = default) =>
        context.RecipientPreferences.AnyAsync(p => p.RecipientId == recipientId && p.Category == category && p.OptedOut, cancellationToken);
}
